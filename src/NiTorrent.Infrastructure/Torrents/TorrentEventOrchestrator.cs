using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentEventOrchestrator
{
    private readonly TorrentUpdatePublisher _updatePublisher;
    private readonly TorrentCatalogSnapshotSynchronizer _catalogSnapshotSynchronizer;
    private readonly BackgroundTaskRunner _backgroundTasks;
    private readonly object _sync = new();
    private Action? _loaded;
    private Action<IReadOnlyList<TorrentSnapshot>>? _updateTorrent;
    private IReadOnlyList<TorrentSnapshot>? _lastPublishedSnapshots;
    private bool _isLoadedRaised;

    public TorrentEventOrchestrator(
        TorrentUpdatePublisher updatePublisher,
        TorrentCatalogSnapshotSynchronizer catalogSnapshotSynchronizer,
        BackgroundTaskRunner backgroundTasks)
    {
        _updatePublisher = updatePublisher;
        _catalogSnapshotSynchronizer = catalogSnapshotSynchronizer;
        _backgroundTasks = backgroundTasks;
    }

    public event Action? Loaded
    {
        add
        {
            if (value is null)
                return;

            bool raiseImmediately;
            lock (_sync)
            {
                _loaded += value;
                raiseImmediately = _isLoadedRaised;
            }

            if (raiseImmediately)
                value.Invoke();
        }
        remove
        {
            if (value is null)
                return;

            lock (_sync)
            {
                _loaded -= value;
            }
        }
    }

    public event Action<IReadOnlyList<TorrentSnapshot>>? UpdateTorrent
    {
        add
        {
            if (value is null)
                return;

            IReadOnlyList<TorrentSnapshot>? replay;
            lock (_sync)
            {
                _updateTorrent += value;
                replay = _lastPublishedSnapshots;
            }

            if (replay is not null)
                value.Invoke(replay);
        }
        remove
        {
            if (value is null)
                return;

            lock (_sync)
            {
                _updateTorrent -= value;
            }
        }
    }

    public void RaiseLoaded()
    {
        Action? handler;
        lock (_sync)
        {
            _isLoadedRaised = true;
            handler = _loaded;
        }

        handler?.Invoke();
    }

    public async Task PublishUpdatesAsync(bool hasEngine, SemaphoreSlim opGate, CancellationToken ct = default)
    {
        Action<IReadOnlyList<TorrentSnapshot>>? handler;
        var snapshots = await _updatePublisher.BuildSnapshotsAsync(hasEngine, opGate, ct).ConfigureAwait(false);

        lock (_sync)
        {
            _lastPublishedSnapshots = snapshots;
            handler = _updateTorrent;
        }

        if (handler is null)
            return;

        if (hasEngine)
        {
            await _catalogSnapshotSynchronizer.SyncAsync(snapshots, ct).ConfigureAwait(false);
            _backgroundTasks.Run(_catalogSnapshotSynchronizer.SaveAsync(CancellationToken.None), "save-catalog");
        }

        handler.Invoke(snapshots);
    }

    public void PublishUpdatesInBackground(bool hasEngine, SemaphoreSlim opGate)
        => _backgroundTasks.Run(PublishUpdatesAsync(hasEngine, opGate, CancellationToken.None), "update-torrent");

    public async Task PublishCachedAsync(CancellationToken ct)
    {
        var snapshots = await _updatePublisher.BuildSnapshotsAsync(hasEngine: false, new SemaphoreSlim(1, 1), ct).ConfigureAwait(false);
        Action<IReadOnlyList<TorrentSnapshot>>? handler;
        lock (_sync)
        {
            _lastPublishedSnapshots = snapshots;
            handler = _updateTorrent;
        }

        handler?.Invoke(snapshots);
    }
}
