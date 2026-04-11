using NiTorrent.Application.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Application-facing maintenance service responsible for saving and shutting down torrent runtime state.
/// </summary>
public sealed class EngineBackedTorrentEngineMaintenanceService : ITorrentEngineMaintenanceService
{
    private readonly TorrentStartupCoordinator _startupCoordinator;
    private readonly TorrentCatalogStore _catalogStore;
    private readonly TorrentEngineStateStore _engineStateStore;
    private readonly TorrentLifecycleExecutor _lifecycleExecutor;

    public EngineBackedTorrentEngineMaintenanceService(
        TorrentStartupCoordinator startupCoordinator,
        TorrentCatalogStore catalogStore,
        TorrentEngineStateStore engineStateStore,
        TorrentLifecycleExecutor lifecycleExecutor)
    {
        _startupCoordinator = startupCoordinator;
        _catalogStore = catalogStore;
        _engineStateStore = engineStateStore;
        _lifecycleExecutor = lifecycleExecutor;
    }

    public Task SaveStateAsync(CancellationToken ct = default)
        => _lifecycleExecutor.RunAsync(async () =>
        {
            await _catalogStore.SaveAsync(ct).ConfigureAwait(false);

            if (_startupCoordinator.Engine is not null)
                await _engineStateStore.SaveAsync(_startupCoordinator.Engine, ct).ConfigureAwait(false);
        }, ct);

    public Task ShutdownAsync(CancellationToken ct = default)
        => SaveStateAsync(ct);
}
