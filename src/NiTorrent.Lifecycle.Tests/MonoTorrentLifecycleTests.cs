using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonoTorrent.BEncoding;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Domain.Torrents;
using NiTorrent.Infrastructure.DI;
using NiTorrent.Infrastructure.Torrents;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class MonoTorrentLifecycleTests
{
    [Fact]
    public async Task MissingMagnetMetadataCannotStartTransferWithoutApplyingFileSelection()
    {
        using var storage = new TemporaryStorage();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddNiTorrentInfrastructure();
        builder.Services.AddSingleton<IAppStorageService>(storage);
        builder.Services.AddSingleton<ISettingsRepository>(new MemorySettings(new()
        {
            EngineSettings = new() { AllowDht = false, AllowLocalPeerDiscovery = false, AllowPortForwarding = false }
        }));
        using var host = builder.Build();
        var repository = host.Services.GetRequiredService<ITorrentRepository>();
        var session = host.Services.GetRequiredService<ITorrentSession>();
        var hash = "1234567890123456789012345678901234567890";
        var item = new TorrentDownload(Guid.NewGuid(), hash, "Legacy magnet", storage.Root)
        {
            Status = TorrentDownloadStatus.Running,
            FileEntries = [new("test.bin", 1, false)]
        };
        await repository.AddAsync(item, new TorrentSource.Magnet("magnet:?xt=urn:btih:" + hash), default);
        await host.StartAsync();
        try
        {
            await SessionFixture.UntilAsync(() => session.GetPreparations().TryGetValue(item.Id, out var p) && p.State == TorrentPreparationState.Failed);
            Assert.Contains("Метаданные", session.GetPreparations()[item.Id].Error);
            Assert.Empty(host.Services.GetRequiredService<TorrentEngineCoordinator>().GetTorrentMap());
            var saved = await repository.GetByIdAsync(item.Id, default);
            Assert.Equal(TorrentDownloadStatus.Running, saved!.Status);
            Assert.False(Assert.Single(saved.FileEntries).IsSelected);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public void ResolvingCoreAndMetadataDoesNotReadStorageOrCreateEngine()
    {
        using var storage = new TemporaryStorage();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddNiTorrentInfrastructure();
        builder.Services.AddSingleton<IAppStorageService>(storage);
        using var host = builder.Build();
        _ = host.Services.GetServices<IHostedService>().ToArray();
        _ = host.Services.GetRequiredService<ITorrentMetadataProvider>();
        Assert.False(Directory.Exists(storage.Root));
        Assert.False(host.Services.GetRequiredService<TorrentEngineCoordinator>().IsInitialized);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RealEngineRestoresWithoutUiAndHonorsDeleteFiles(bool deleteFiles, bool failFirstDeletion)
    {
        using var storage = new TemporaryStorage();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddNiTorrentInfrastructure();
        builder.Services.AddSingleton<IAppStorageService>(storage);
        builder.Services.AddSingleton<ISettingsRepository>(new MemorySettings(new()
        {
            EngineSettings = new() { AllowDht = false, AllowLocalPeerDiscovery = false, AllowPortForwarding = false }
        }));
        using var host = builder.Build();
        var repository = host.Services.GetRequiredService<ITorrentRepository>();
        var session = host.Services.GetRequiredService<ITorrentSession>();
        var info = new BEncodedDictionary
        {
            ["name"] = new BEncodedString("test.bin"),
            ["length"] = new BEncodedNumber(1),
            ["piece length"] = new BEncodedNumber(16384),
            ["pieces"] = new BEncodedString(System.Security.Cryptography.SHA1.HashData([42]))
        };
        var metadata = new BEncodedDictionary { ["info"] = info }.Encode();
        var torrent = MonoTorrent.Torrent.Load(metadata);
        var item = new TorrentDownload(Guid.NewGuid(), torrent.InfoHashes.V1OrV2.ToHex(), "Test", storage.Root)
        {
            Status = TorrentDownloadStatus.Paused,
            FileEntries = [new("test.bin", 1, true)]
        };
        await repository.AddAsync(item, new TorrentSource.TorrentBytes(metadata), default);
        var file = Path.Combine(storage.Root, "test.bin");
        await File.WriteAllBytesAsync(file, [42]);
        await host.StartAsync();
        try
        {
            await SessionFixture.UntilAsync(() => session.GetPreparations().TryGetValue(item.Id, out var p) && p.State == TorrentPreparationState.Ready);
            var state = host.Services.GetRequiredService<ITorrentRuntimeStateSource>();
            await SessionFixture.UntilAsync(() => state.TryGet(item.Id, out var s) && s.State == NiTorrent.Application.Torrents.Enum.TorrentLifecycleState.Stopped);
            var coordinator = host.Services.GetRequiredService<TorrentEngineCoordinator>();
            Assert.Single(coordinator.GetTorrentMap());
            if (failFirstDeletion)
            {
                using (var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await session.DeleteAsync(item.Id, true, default);
                    await SessionFixture.UntilAsync(() => session.GetPreparations()[item.Id].State == TorrentPreparationState.Failed);
                    Assert.True(File.Exists(file));
                    Assert.Equal(TorrentDownloadStatus.Deleted, (await repository.GetByIdAsync(item.Id, default))!.Status);
                    Assert.Empty(coordinator.GetTorrentMap());
                }
                session.Retry(item.Id);
            }
            else await session.DeleteAsync(item.Id, deleteFiles, default);
            await SessionFixture.UntilAsync(() => !session.GetPreparations().ContainsKey(item.Id));
            Assert.Null(await repository.GetByIdAsync(item.Id, default));
            Assert.Empty(coordinator.GetTorrentMap());
            Assert.Equal(!deleteFiles, File.Exists(file));
        }
        finally { await host.StopAsync(); }
        Assert.False(host.Services.GetRequiredService<TorrentEngineCoordinator>().IsInitialized);
    }
}
