using Microsoft.Extensions.Logging.Abstractions;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;
using NiTorrent.Infrastructure.Torrents;
using Xunit;

namespace NiTorrent.Infrastructure.Tests.Torrents;

public sealed class TorrentCatalogStoreRuntimeProjectionTests
{
    [Fact]
    public async Task UpsertAndGetEntries_PreservesLiveRuntimeRates_WithinCurrentProcess()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new TorrentCatalogStore(NullLogger<TorrentCatalogStore>.Instance, new TestStorage(root));
            var entry = CreateEntry(
                source: TorrentStatusSource.Live,
                downloadRate: 2048,
                uploadRate: 256,
                lifecycleState: TorrentLifecycleState.Downloading,
                phase: TorrentPhase.Downloading);

            await store.UpsertEntryAsync(entry);
            await store.SaveAsync(force: true, CancellationToken.None);

            var restored = Assert.Single(await store.GetEntriesAsync());
            Assert.True(restored.Runtime.IsEngineBacked);
            Assert.Equal(2048, restored.Runtime.DownloadRateBytesPerSecond);
            Assert.Equal(256, restored.Runtime.UploadRateBytesPerSecond);
            Assert.Equal(TorrentStatusSource.Live, restored.LastKnownStatus.Source);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReloadFromDisk_DoesNotExposeStaleLiveRuntimeRates()
    {
        var root = CreateTempDirectory();
        try
        {
            var first = new TorrentCatalogStore(NullLogger<TorrentCatalogStore>.Instance, new TestStorage(root));
            var entry = CreateEntry(
                source: TorrentStatusSource.Live,
                downloadRate: 4096,
                uploadRate: 512,
                lifecycleState: TorrentLifecycleState.Downloading,
                phase: TorrentPhase.Downloading);

            await first.UpsertEntryAsync(entry);
            await first.SaveAsync(force: true, CancellationToken.None);

            var second = new TorrentCatalogStore(NullLogger<TorrentCatalogStore>.Instance, new TestStorage(root));
            var restored = Assert.Single(await second.GetEntriesAsync());

            Assert.False(restored.Runtime.IsEngineBacked);
            Assert.Equal(0, restored.Runtime.DownloadRateBytesPerSecond);
            Assert.Equal(0, restored.Runtime.UploadRateBytesPerSecond);
            Assert.Equal(TorrentStatusSource.Cached, restored.LastKnownStatus.Source);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static TorrentEntry CreateEntry(
        TorrentStatusSource source,
        long downloadRate,
        long uploadRate,
        TorrentLifecycleState lifecycleState,
        TorrentPhase phase)
    {
        var id = TorrentId.New();
        var isEngineBacked = source == TorrentStatusSource.Live;

        return new TorrentEntry(
            id,
            new TorrentKey($"k-{id.Value:N}"),
            "torrent",
            1024,
            "C:\\downloads",
            DateTimeOffset.UtcNow,
            TorrentIntent.Running,
            lifecycleState,
            new TorrentRuntimeState(
                lifecycleState,
                IsComplete: false,
                Progress: 10,
                DownloadRateBytesPerSecond: downloadRate,
                UploadRateBytesPerSecond: uploadRate,
                Error: null,
                IsEngineBacked: isEngineBacked),
            new TorrentStatus(
                phase,
                IsComplete: false,
                Progress: 10,
                DownloadRateBytesPerSecond: downloadRate,
                UploadRateBytesPerSecond: uploadRate,
                Error: null,
                Source: source),
            HasMetadata: true,
            SelectedFiles: [],
            PerTorrentSettings: null,
            DeferredActions: []);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"NiTorrentTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestStorage(string root) : IAppStorageService
    {
        public string GetLocalPath(string relative) => Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar));
        public string GetCachePath(string relative) => Path.Combine(root, "cache", relative.Replace('\\', Path.DirectorySeparatorChar));

        public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

        public void EnsureParentDirectory(string filePath)
        {
            var parent = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
        }
    }
}
