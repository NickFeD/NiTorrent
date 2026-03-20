using Microsoft.Extensions.Logging;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentUpdatePublisher
{
    private readonly ILogger<TorrentUpdatePublisher> _logger;
    private readonly TorrentCatalogStore _catalogStore;
    private readonly TorrentSnapshotFactory _snapshotFactory;
    private readonly TorrentRuntimeRegistry _runtimeRegistry;

    public TorrentUpdatePublisher(
        ILogger<TorrentUpdatePublisher> logger,
        TorrentCatalogStore catalogStore,
        TorrentSnapshotFactory snapshotFactory,
        TorrentRuntimeRegistry runtimeRegistry)
    {
        _logger = logger;
        _catalogStore = catalogStore;
        _snapshotFactory = snapshotFactory;
        _runtimeRegistry = runtimeRegistry;
    }

    public async Task<IReadOnlyList<TorrentSnapshot>> BuildSnapshotsAsync(
        bool hasEngine,
        SemaphoreSlim opGate,
        CancellationToken ct)
    {
        var cachedSnapshots = DeduplicateSnapshots(await _catalogStore.BuildCachedSnapshotsAsync(ct).ConfigureAwait(false));

        if (!hasEngine)
            return cachedSnapshots;

        List<(TorrentId id, MonoTorrent.Client.TorrentManager manager)> managers;

        await opGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            managers = _runtimeRegistry.Snapshot()
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }
        finally
        {
            opGate.Release();
        }

        var merged = new Dictionary<TorrentId, TorrentSnapshot>();

        foreach (var cached in cachedSnapshots)
            merged[cached.Id] = cached;

        foreach (var (id, manager) in managers)
        {
            var addedAt = await _catalogStore.TryGetAddedAtUtcAsync(id, ct).ConfigureAwait(false);
            var live = _snapshotFactory.Create(id, manager, addedAtUtc: addedAt ?? DateTimeOffset.UtcNow);

            if (merged.TryGetValue(live.Id, out var cached))
                merged[live.Id] = ReconcileStartupSnapshot(cached, live);
            else
                merged[live.Id] = live;
        }

        return merged.Values
            .OrderBy(s => s.AddedAtUtc)
            .ToList();
    }

    private static IReadOnlyList<TorrentSnapshot> DeduplicateSnapshots(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        var merged = new Dictionary<TorrentId, TorrentSnapshot>();

        foreach (var snapshot in snapshots.OrderBy(s => s.AddedAtUtc))
            merged[snapshot.Id] = snapshot;

        return merged.Values
            .OrderBy(s => s.AddedAtUtc)
            .ToList();
    }

    private static TorrentSnapshot ReconcileStartupSnapshot(TorrentSnapshot cached, TorrentSnapshot live)
    {
        if (cached.Status.Source != TorrentSnapshotSource.Cached)
            return live;

        if (live.Status.Phase is not (TorrentPhase.Stopped or TorrentPhase.Paused))
            return live;

        if (live.Status.DownloadRateBytesPerSecond != 0 || live.Status.UploadRateBytesPerSecond != 0)
            return live;

        var cachedRepresentsRunning = cached.Status.Phase is
            TorrentPhase.Downloading or
            TorrentPhase.Seeding or
            TorrentPhase.Checking or
            TorrentPhase.FetchingMetadata or
            TorrentPhase.WaitingForEngine or
            TorrentPhase.EngineStarting;

        if (!cachedRepresentsRunning && cached.Status.Phase != TorrentPhase.Paused)
            return live;

        return live with
        {
            Status = live.Status with
            {
                Phase = cached.Status.Phase,
                Progress = cached.Status.Progress
            }
        };
    }

    public async Task PublishCachedAsync(Action<IReadOnlyList<TorrentSnapshot>>? handler, CancellationToken ct)
    {
        if (handler is null)
            return;

        try
        {
            var snapshots = DeduplicateSnapshots(await _catalogStore.BuildCachedSnapshotsAsync(ct).ConfigureAwait(false));
            handler.Invoke(snapshots);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish cached snapshots");
        }
    }
}
