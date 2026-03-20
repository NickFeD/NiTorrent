using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentCommandExecutor
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger<TorrentCommandExecutor> _logger;
    private readonly TorrentCatalogStore _catalogStore;
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly TorrentEngineStateStore _engineStateStore;

    public TorrentCommandExecutor(
        ILogger<TorrentCommandExecutor> logger,
        TorrentCatalogStore catalogStore,
        TorrentRuntimeRegistry runtimeRegistry,
        TorrentEngineStateStore engineStateStore)
    {
        _logger = logger;
        _catalogStore = catalogStore;
        _runtimeRegistry = runtimeRegistry;
        _engineStateStore = engineStateStore;
    }

    public async Task StartAsync(
        TorrentId id,
        bool engineReady,
        SemaphoreSlim opGate,
        TorrentCommandQueue commandQueue,
        Func<CancellationToken, Task> ensureStartedAsync,
        Action publishTorrentUpdates,
        Action<Task, string> fireAndForget,
        CancellationToken ct)
    {
        await _catalogStore.SetShouldRunAsync(id, true, ct).ConfigureAwait(false);

        if (!engineReady)
        {
            commandQueue.SetDesiredRunning(id, true);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
            publishTorrentUpdates();
            fireAndForget(ensureStartedAsync(ct), "ensure-started");
            return;
        }

        var manager = await GetManagerAsync(id, opGate, ct).ConfigureAwait(false);
        if (manager is null)
        {
            commandQueue.SetDesiredRunning(id, true);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
            publishTorrentUpdates();
            return;
        }

        try
        {
            await manager.StartAsync().ConfigureAwait(false);
            publishTorrentUpdates();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start torrent {TorrentId}", id.Value);
            throw;
        }
    }

    public async Task PauseAsync(
        TorrentId id,
        bool engineReady,
        SemaphoreSlim opGate,
        TorrentCommandQueue commandQueue,
        Func<CancellationToken, Task> ensureStartedAsync,
        Action publishTorrentUpdates,
        Action<Task, string> fireAndForget,
        CancellationToken ct)
    {
        _logger.LogInformation("Pausing torrent {TorrentId}", id.Value);
        await _catalogStore.SetShouldRunAsync(id, false, ct).ConfigureAwait(false);

        if (!engineReady)
        {
            commandQueue.SetDesiredRunning(id, false);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
            publishTorrentUpdates();
            fireAndForget(ensureStartedAsync(ct), "ensure-started");
            return;
        }

        var manager = await GetManagerAsync(id, opGate, ct).ConfigureAwait(false);
        if (manager is null)
        {
            commandQueue.SetDesiredRunning(id, false);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
            publishTorrentUpdates();
            return;
        }

        await manager.PauseAsync().ConfigureAwait(false);
        publishTorrentUpdates();
    }

    public async Task StopAsync(
        TorrentId id,
        bool engineReady,
        SemaphoreSlim opGate,
        TorrentCommandQueue commandQueue,
        Func<CancellationToken, Task> ensureStartedAsync,
        Action publishTorrentUpdates,
        Action<Task, string> fireAndForget,
        CancellationToken ct)
    {
        _logger.LogInformation("Stopping torrent {TorrentId}", id.Value);
        await _catalogStore.SetShouldRunAsync(id, false, ct).ConfigureAwait(false);

        if (!engineReady)
        {
            commandQueue.SetDesiredRunning(id, false);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
            publishTorrentUpdates();
            fireAndForget(ensureStartedAsync(ct), "ensure-started");
            return;
        }

        var manager = await GetManagerAsync(id, opGate, ct).ConfigureAwait(false);
        if (manager is null)
        {
            commandQueue.SetDesiredRunning(id, false);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
            publishTorrentUpdates();
            return;
        }

        await manager.StopAsync(StopTimeout).ConfigureAwait(false);
        publishTorrentUpdates();
    }

    public async Task RemoveAsync(
        TorrentId id,
        bool deleteDownloadedData,
        bool engineReady,
        ClientEngine? engine,
        SemaphoreSlim opGate,
        TorrentCommandQueue commandQueue,
        Func<CancellationToken, Task> ensureStartedAsync,
        Action publishTorrentUpdates,
        Action<Task, string> fireAndForget,
        CancellationToken ct)
    {
        _logger.LogInformation("Removing torrent {TorrentId}. Delete data: {DeleteDownloadedData}", id.Value, deleteDownloadedData);

        if (!engineReady)
        {
            var cached = await _catalogStore.TryGetCachedAsync(id, ct).ConfigureAwait(false);
            if (cached is null)
                return;

            await _catalogStore.RemoveAndRememberDeletionAsync(id, cached.Key, deleteDownloadedData, ct).ConfigureAwait(false);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
            publishTorrentUpdates();
            fireAndForget(ensureStartedAsync(ct), "ensure-started");
            return;
        }

        if (engine is null)
            throw new InvalidOperationException("Torrent engine is not initialized yet.");

        await ensureStartedAsync(ct).ConfigureAwait(false);

        var manager = await GetManagerAsync(id, opGate, ct).ConfigureAwait(false);
        if (manager is null)
            return;

        try
        {
            await manager.StopAsync(StopTimeout).ConfigureAwait(false);

            var mode = deleteDownloadedData ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly;
            await engine.RemoveAsync(manager, mode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove torrent {TorrentId}", id.Value);
            throw;
        }

        await opGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _runtimeRegistry.Remove(id);
        }
        finally
        {
            opGate.Release();
        }

        await _catalogStore.RemoveAsync(id, ct).ConfigureAwait(false);
        await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
        await _engineStateStore.SaveAsync(engine, CancellationToken.None).ConfigureAwait(false);
        publishTorrentUpdates();
    }

    private async Task<TorrentManager?> GetManagerAsync(TorrentId id, SemaphoreSlim opGate, CancellationToken ct)
    {
        TorrentManager? manager;
        await opGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _runtimeRegistry.TryGet(id, out manager);
        }
        finally
        {
            opGate.Release();
        }

        return manager;
    }
}
