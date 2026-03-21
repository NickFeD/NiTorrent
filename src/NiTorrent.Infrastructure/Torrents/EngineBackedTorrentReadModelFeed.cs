using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Transitional read feed that listens to the infrastructure event orchestrator directly
/// and rebuilds snapshots from infrastructure-owned sources instead of calling ITorrentService.
/// </summary>
public sealed class EngineBackedTorrentReadModelFeed : ITorrentReadModelFeed, IDisposable
{
    private readonly TorrentEventOrchestrator _eventOrchestrator;
    private readonly TorrentUpdatePublisher _updatePublisher;
    private readonly TorrentStartupCoordinator _startupCoordinator;
    private readonly TorrentRuntimeContext _runtimeContext;
    private readonly object _sync = new();
    private IReadOnlyList<TorrentSnapshot> _current = [];
    private event Action<IReadOnlyList<TorrentSnapshot>>? _updated;

    public EngineBackedTorrentReadModelFeed(
        TorrentEventOrchestrator eventOrchestrator,
        TorrentUpdatePublisher updatePublisher,
        TorrentStartupCoordinator startupCoordinator,
        TorrentRuntimeContext runtimeContext)
    {
        _eventOrchestrator = eventOrchestrator;
        _updatePublisher = updatePublisher;
        _startupCoordinator = startupCoordinator;
        _runtimeContext = runtimeContext;

        _eventOrchestrator.UpdateTorrent += OnUpdated;
        Refresh();
    }

    public event Action<IReadOnlyList<TorrentSnapshot>>? Updated
    {
        add
        {
            _updated += value;
            value?.Invoke(Current);
        }
        remove => _updated -= value;
    }

    public IReadOnlyList<TorrentSnapshot> Current
    {
        get
        {
            lock (_sync)
                return _current;
        }
    }

    public void Refresh()
        => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        var snapshots = await _updatePublisher
            .BuildSnapshotsAsync(_startupCoordinator.Engine is not null, _runtimeContext.OperationGate, CancellationToken.None)
            .ConfigureAwait(false);

        OnUpdated(snapshots);
    }

    private void OnUpdated(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        lock (_sync)
            _current = snapshots;

        _updated?.Invoke(snapshots);
    }

    public void Dispose()
    {
        _eventOrchestrator.UpdateTorrent -= OnUpdated;
    }
}
