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
        if (!hasEngine)
            return await _catalogStore.BuildCachedSnapshotsAsync(ct).ConfigureAwait(false);

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

        var snapshots = new List<TorrentSnapshot>(managers.Count);
        foreach (var (id, manager) in managers)
        {
            var addedAt = await _catalogStore.TryGetAddedAtUtcAsync(id, ct).ConfigureAwait(false);
            snapshots.Add(_snapshotFactory.Create(id, manager, addedAtUtc: addedAt ?? DateTimeOffset.UtcNow));
        }

        return snapshots;
    }

    public async Task PublishCachedAsync(Action<IReadOnlyList<TorrentSnapshot>>? handler, CancellationToken ct)
    {
        if (handler is null)
            return;

        try
        {
            var snapshots = await _catalogStore.BuildCachedSnapshotsAsync(ct).ConfigureAwait(false);
            handler.Invoke(snapshots);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish cached snapshots");
        }
    }
}
