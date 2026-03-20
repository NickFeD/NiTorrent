using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentEventOrchestrator
{
    private readonly TorrentUpdatePublisher _updatePublisher;
    private readonly TorrentCatalogSnapshotSynchronizer _catalogSnapshotSynchronizer;
    private readonly BackgroundTaskRunner _backgroundTasks;

    public TorrentEventOrchestrator(
        TorrentUpdatePublisher updatePublisher,
        TorrentCatalogSnapshotSynchronizer catalogSnapshotSynchronizer,
        BackgroundTaskRunner backgroundTasks)
    {
        _updatePublisher = updatePublisher;
        _catalogSnapshotSynchronizer = catalogSnapshotSynchronizer;
        _backgroundTasks = backgroundTasks;
    }

    public event Action? Loaded;
    public event Action<IReadOnlyList<TorrentSnapshot>>? UpdateTorrent;

    public void RaiseLoaded()
        => Loaded?.Invoke();

    public async Task PublishUpdatesAsync(bool hasEngine, SemaphoreSlim opGate, CancellationToken ct = default)
    {
        var handler = UpdateTorrent;
        if (handler is null)
            return;

        var snapshots = await _updatePublisher.BuildSnapshotsAsync(hasEngine, opGate, ct).ConfigureAwait(false);

        if (hasEngine)
        {
            await _catalogSnapshotSynchronizer.SyncAsync(snapshots, ct).ConfigureAwait(false);
            _backgroundTasks.Run(_catalogSnapshotSynchronizer.SaveAsync(CancellationToken.None), "save-catalog");
        }

        handler.Invoke(snapshots);
    }

    public void PublishUpdatesInBackground(bool hasEngine, SemaphoreSlim opGate)
        => _backgroundTasks.Run(PublishUpdatesAsync(hasEngine, opGate, CancellationToken.None), "update-torrent");

    public Task PublishCachedAsync(CancellationToken ct)
        => _updatePublisher.PublishCachedAsync(UpdateTorrent, ct);
}
