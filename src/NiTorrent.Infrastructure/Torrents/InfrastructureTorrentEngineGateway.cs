using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class InfrastructureTorrentEngineGateway(
    TorrentStartupCoordinator startupCoordinator,
    TorrentRuntimeContext runtimeContext,
    TorrentLifecycleExecutor lifecycleExecutor,
    TorrentRuntimeRegistry runtimeRegistry,
    PeerEndpointConnectionCooldown peerEndpointCooldown,
    TorrentEngineStateStore engineStateStore,
    TorrentEventOrchestrator eventOrchestrator,
    BackgroundTaskRunner backgroundTasks) : ITorrentEngineGateway
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    public Task<bool> StartAsync(TorrentId id, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            var manager = await GetManagerAsync(id, ct).ConfigureAwait(false);
            if (manager is null)
                return false;

            peerEndpointCooldown.ResetForTorrent(id);
            await manager.StartAsync().ConfigureAwait(false);
            PublishRuntimeChanged();
            return true;
        }, ct);

    public Task<bool> PauseAsync(TorrentId id, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            var manager = await GetManagerAsync(id, ct).ConfigureAwait(false);
            if (manager is null)
                return false;

            await manager.PauseAsync().ConfigureAwait(false);
            PublishRuntimeChanged();
            return true;
        }, ct);

    public Task StopAsync(TorrentId id, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            var manager = await GetManagerAsync(id, ct).ConfigureAwait(false);
            if (manager is null)
                return;

            await manager.StopAsync(StopTimeout).ConfigureAwait(false);
            PublishRuntimeChanged();
        }, ct);

    public Task<bool> RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            var engine = startupCoordinator.Engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");
            var manager = await GetManagerAsync(id, ct).ConfigureAwait(false);
            if (manager is null)
                return false;

            peerEndpointCooldown.Unregister(id, manager);
            await manager.StopAsync(StopTimeout).ConfigureAwait(false);

            var mode = deleteData ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly;
            await engine.RemoveAsync(manager, mode).ConfigureAwait(false);

            await runtimeContext.OperationGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                runtimeRegistry.Remove(id);
            }
            finally
            {
                runtimeContext.OperationGate.Release();
            }

            backgroundTasks.Run(engineStateStore.SaveAsync(engine, CancellationToken.None), "save-engine-state");
            PublishRuntimeChanged();
            return true;
        }, ct);

    private Task EnsureStartedAsync(CancellationToken ct = default)
        => startupCoordinator.EnsureStartedAsync(runtimeContext.OperationGate, eventOrchestrator.RaiseLoaded, ct);

    private async Task<TorrentManager?> GetManagerAsync(TorrentId id, CancellationToken ct)
    {
        await runtimeContext.OperationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            runtimeRegistry.TryGet(id, out var manager);
            return manager;
        }
        finally
        {
            runtimeContext.OperationGate.Release();
        }
    }

    private void PublishRuntimeChanged()
        => eventOrchestrator.InvalidateRuntime();
}
