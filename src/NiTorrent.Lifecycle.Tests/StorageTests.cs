using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Settings.Enums;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;
using NiTorrent.Infrastructure.Settings;
using NiTorrent.Infrastructure.Torrents;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class StorageTests
{
    private static TorrentDownload Item(string path) => new(Guid.NewGuid(), Guid.NewGuid().ToString("N"), "Example", path)
    {
        Status = TorrentDownloadStatus.Running,
        FileEntries = [new("test.bin", 6, true)]
    };
    private static string Catalog(TemporaryStorage storage) => storage.GetLocalPath(@"Torrents\torrent_downloads.json");

    [Fact]
    public async Task FailedWriteRetainsLatestFullSnapshotAndPreservesPreviousFile()
    {
        using var storage = new TemporaryStorage();
        var repository = new JsonTorrentRepository(storage);
        var first = Item(storage.Root);
        var second = Item(storage.Root);
        await repository.AddAsync(first, new TorrentSource.TorrentBytes([1]), default);
        await repository.AddAsync(second, new TorrentSource.TorrentBytes([2]), default);
        var original = await File.ReadAllTextAsync(Catalog(storage));
        using (var locked = new FileStream(Catalog(storage), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            first.Status = TorrentDownloadStatus.Paused;
            await repository.UpdateAsync(first, default);
            second.Status = TorrentDownloadStatus.Paused;
            await repository.UpdateAsync(second, default);
            Assert.True(repository.HasPendingWrites);
            Assert.Equal(original, await File.ReadAllTextAsync(Catalog(storage)));
            Assert.Equal(TorrentDownloadStatus.Paused, (await repository.GetByIdAsync(first.Id, default))!.Status);
            var error = await Record.ExceptionAsync(() => repository.FlushAsync(default));
            Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected flush result: {error}");
        }
        await repository.FlushAsync(default);
        Assert.False(repository.HasPendingWrites);
        var reloaded = new JsonTorrentRepository(storage);
        Assert.All(await reloaded.GetAllAsync(default), x => Assert.Equal(TorrentDownloadStatus.Paused, x.Status));
        Assert.False(File.Exists(Catalog(storage) + ".tmp"));
    }

    [Fact]
    public async Task CorruptCatalogIsNotOverwrittenByLoadOrFlush()
    {
        using var storage = new TemporaryStorage();
        storage.EnsureParentDirectory(Catalog(storage));
        const string broken = "{broken-json";
        await File.WriteAllTextAsync(Catalog(storage), broken);
        var repository = new JsonTorrentRepository(storage);
        await Assert.ThrowsAsync<JsonException>(() => repository.LoadingAsync(default));
        await Assert.ThrowsAsync<JsonException>(() => repository.FlushAsync(default));
        Assert.Equal(broken, await File.ReadAllTextAsync(Catalog(storage)));
    }

    [Fact]
    public async Task LegacyCatalogWithoutDeleteFlagKeepsStatusSourceAndFileSelection()
    {
        using var storage = new TemporaryStorage();
        var id = Guid.NewGuid();
        storage.EnsureParentDirectory(Catalog(storage));
        await File.WriteAllTextAsync(Catalog(storage), JsonSerializer.Serialize(new
        {
            Items = new[] { new { Id = id, InfoHash = "hash", Name = "Old", SavePath = storage.Root, Status = 2,
                FileEntries = new[] { new { FullPath = "test.bin", SizeByte = 6L, IsSelected = false } },
                Source = new { Kind = 3, Value = Convert.ToBase64String([1, 2, 3]) } } }
        }));
        var repository = new JsonTorrentRepository(storage);
        var restored = await repository.GetForRestoreAsync(id, default);
        Assert.Equal(TorrentDownloadStatus.Paused, restored!.Torrent.Status);
        Assert.False(restored.Torrent.DeleteFiles);
        Assert.False(Assert.Single(restored.Torrent.FileEntries).IsSelected);
        Assert.Equal(6, restored.Torrent.Size);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<TorrentSource.TorrentBytes>(restored.TorrentSource).Bytes);
    }

    [Fact]
    public async Task InvalidSourceIsIsolatedToOneTorrentDuringRestore()
    {
        await using var f = new SessionFixture();
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        var path = Catalog(f.Storage);
        f.Storage.EnsureParentDirectory(path);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            Items = new[] { bad, good }.Select(id => new { Id = id, InfoHash = id.ToString("N"), Name = "Example", SavePath = f.Storage.Root,
                Status = 1, FileEntries = Array.Empty<object>(), Source = new { Kind = 3, Value = id == bad ? "invalid!" : "AQID" } })
        }));
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Session.GetPreparations().TryGetValue(bad, out var b) && b.State == NiTorrent.Application.Torrents.Abstract.TorrentPreparationState.Failed
            && f.Session.GetPreparations().TryGetValue(good, out var g) && g.State == NiTorrent.Application.Torrents.Abstract.TorrentPreparationState.Ready);
        Assert.NotNull(await f.Repository.GetByIdAsync(bad, default));
        Assert.DoesNotContain(("Add", bad), f.Runtime.Calls);
        Assert.True(f.Runtime.Managers[good]);
    }

    [Fact]
    public async Task SourceCopyIsKeptUntilCatalogDeletionIsDurable()
    {
        using var storage = new TemporaryStorage();
        var original = Path.Combine(storage.Root, "original.torrent");
        storage.EnsureDirectory(storage.Root);
        await File.WriteAllBytesAsync(original, [1, 2, 3]);
        var repository = new JsonTorrentRepository(storage);
        var item = Item(storage.Root);
        await repository.AddAsync(item, new TorrentSource.TorrentFile(original), default);
        var copy = Assert.IsType<TorrentSource.TorrentFile>((await repository.GetForRestoreAsync(item.Id, default))!.TorrentSource).Path;
        using (var locked = new FileStream(Catalog(storage), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await repository.DeleteAsync(item.Id, default);
            Assert.True(File.Exists(copy));
            Assert.True(repository.HasPendingWrites);
        }
        await repository.FlushAsync(default);
        Assert.False(File.Exists(copy));
        Assert.True(File.Exists(original));
        Assert.Null(await new JsonTorrentRepository(storage).GetByIdAsync(item.Id, default));
    }

    [Fact]
    public async Task SettingsSaveRetriesAfterFailureAndRemainsReadableByExistingLoader()
    {
        using var storage = new TemporaryStorage();
        var settings = new AppJsonSettings();
        var repository = new SettingsRepository(new Lazy<AppJsonSettings>(() => settings), storage, NullLogger<SettingsRepository>.Instance);
        var path = storage.GetLocalPath("torrent_settings.json");
        await repository.SaveAsync(new AppSettings { CloseBehavior = AppCloseBehavior.ExitApplication }, default);
        var original = await File.ReadAllTextAsync(path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await repository.SaveAsync(new AppSettings { CloseBehavior = AppCloseBehavior.MinimizeToTray,
                EngineSettings = new() { MaximumDownloadRate = 1234 } }, default);
            Assert.Equal(original, await File.ReadAllTextAsync(path));
            Assert.Equal(AppCloseBehavior.MinimizeToTray, (await repository.GetAsync(default)).CloseBehavior);
            var error = await Record.ExceptionAsync(() => repository.FlushAsync(default));
            Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected flush result: {error}");
        }
        await repository.FlushAsync(default);
        var loaded = Nucs.JsonSettings.JsonSettings.Load<AppJsonSettings>(path);
        Assert.Equal(AppCloseBehavior.MinimizeToTray, loaded.CloseBehavior);
        Assert.Equal(1234, loaded.EngineSettings.MaximumDownloadRate);
    }
}
