using NiTorrent.Domain.Torrents;
using Xunit;

namespace NiTorrent.Domain.Tests.Torrents;

public sealed class TorrentCollectionRestorePolicyTests
{
    [Fact]
    public void ApplyRuntimeFacts_IntentPaused_DoesNotElevateToDownloading()
    {
        var id = TorrentId.New();
        var entry = CreateEntry(id, TorrentIntent.Paused, TorrentLifecycleState.Paused);
        var facts = new[]
        {
            new TorrentRuntimeFact(
                id,
                new TorrentKey("k1"),
                "runtime-name",
                1024,
                "C:\\data",
                new TorrentRuntimeState(
                    TorrentLifecycleState.Downloading,
                    IsComplete: false,
                    Progress: 55,
                    DownloadRateBytesPerSecond: 100,
                    UploadRateBytesPerSecond: 50,
                    Error: null,
                    IsEngineBacked: true))
        };

        var updated = TorrentCollectionRestorePolicy.ApplyRuntimeFacts(new[] { entry }, facts).Single();

        Assert.Equal(TorrentIntent.Paused, updated.Intent);
        Assert.Equal(TorrentLifecycleState.Paused, updated.Runtime.LifecycleState);
        Assert.Equal(TorrentPhase.Paused, updated.LastKnownStatus.Phase);
        Assert.Equal(0, updated.Runtime.DownloadRateBytesPerSecond);
        Assert.Equal(0, updated.Runtime.UploadRateBytesPerSecond);
    }

    [Fact]
    public void ApplyRuntimeFacts_IntentRunning_AcceptsRuntimeRefinement()
    {
        var id = TorrentId.New();
        var entry = CreateEntry(id, TorrentIntent.Running, TorrentLifecycleState.WaitingForEngine);
        var facts = new[]
        {
            new TorrentRuntimeFact(
                id,
                new TorrentKey("k2"),
                "runtime-name",
                2048,
                "C:\\downloads",
                new TorrentRuntimeState(
                    TorrentLifecycleState.Seeding,
                    IsComplete: true,
                    Progress: 100,
                    DownloadRateBytesPerSecond: 0,
                    UploadRateBytesPerSecond: 400,
                    Error: null,
                    IsEngineBacked: true))
        };

        var updated = TorrentCollectionRestorePolicy.ApplyRuntimeFacts(new[] { entry }, facts).Single();

        Assert.Equal(TorrentIntent.Running, updated.Intent);
        Assert.Equal(TorrentLifecycleState.Seeding, updated.Runtime.LifecycleState);
        Assert.Equal(TorrentPhase.Seeding, updated.LastKnownStatus.Phase);
        Assert.True(updated.Runtime.IsEngineBacked);
    }

    private static TorrentEntry CreateEntry(TorrentId id, TorrentIntent intent, TorrentLifecycleState lifecycle)
    {
        var runtime = new TorrentRuntimeState(
            lifecycle,
            IsComplete: false,
            Progress: 0,
            DownloadRateBytesPerSecond: 0,
            UploadRateBytesPerSecond: 0,
            Error: null,
            IsEngineBacked: false);

        return new TorrentEntry(
            id,
            new TorrentKey("initial"),
            "name",
            1,
            "C:\\initial",
            DateTimeOffset.UtcNow,
            intent,
            lifecycle,
            runtime,
            new TorrentStatus(TorrentPhase.WaitingForEngine, false, 0, 0, 0),
            HasMetadata: true,
            SelectedFiles: Array.Empty<string>(),
            PerTorrentSettings: null,
            DeferredActions: Array.Empty<DeferredAction>());
    }
}

