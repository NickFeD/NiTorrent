using Microsoft.Extensions.Logging;
using MonoTorrent.Client;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentStartupCoordinator
{
    private readonly ILogger<TorrentStartupCoordinator> _logger;
    private readonly TorrentCatalogStore _catalogStore;
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly TorrentEngineFactory _engineFactory;
    private readonly PeerEndpointConnectionCooldown _peerEndpointCooldown;

    private Task? _initTask;

    public TorrentStartupCoordinator(
        ILogger<TorrentStartupCoordinator> logger,
        TorrentCatalogStore catalogStore,
        TorrentRuntimeRegistry runtimeRegistry,
        TorrentEngineFactory engineFactory,
        PeerEndpointConnectionCooldown peerEndpointCooldown)
    {
        _logger = logger;
        _catalogStore = catalogStore;
        _runtimeRegistry = runtimeRegistry;
        _engineFactory = engineFactory;
        _peerEndpointCooldown = peerEndpointCooldown;
    }

    public ClientEngine? Engine { get; private set; }

    public bool IsReady { get; private set; }


    public async Task ShutdownAsync(
        SemaphoreSlim opGate,
        CancellationToken ct = default)
    {
        var pendingInit = _initTask;
        if (pendingInit is not null)
        {
            try
            {
                await pendingInit.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Torrent engine init failed before shutdown");
            }
        }

        ClientEngine? engineToDispose = null;

        await opGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Engine is null)
            {
                IsReady = false;
                _initTask = null;
                _runtimeRegistry.Clear();
                return;
            }

            try
            {
                foreach (var (torrentId, manager) in _runtimeRegistry.Snapshot())
                    _peerEndpointCooldown.Unregister(torrentId, manager);

                await Engine.StopAllAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop torrent engine cleanly");
            }

            engineToDispose = Engine;
            Engine = null;
            IsReady = false;
            _initTask = null;
            _runtimeRegistry.Clear();
        }
        finally
        {
            opGate.Release();
        }

        if (engineToDispose is not null)
        {
            try
            {
                engineToDispose.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose torrent engine cleanly");
            }
        }
    }

    public Task EnsureStartedAsync(
        SemaphoreSlim opGate,
        Action? onLoaded,
        CancellationToken ct = default)
    {
        if (_initTask is not null)
            return _initTask;

        return StartOnceAsync(ct);

        async Task StartOnceAsync(CancellationToken ct2)
        {
            Task initTask;

            await opGate.WaitAsync(ct2).ConfigureAwait(false);
            try
            {
                _initTask ??= LoadEngineInternalAsync(opGate, onLoaded, ct2);
                initTask = _initTask;
            }
            finally
            {
                opGate.Release();
            }

            try
            {
                await initTask.ConfigureAwait(false);
            }
            catch
            {
                await opGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    if (ReferenceEquals(_initTask, initTask))
                        _initTask = null;
                }
                finally
                {
                    opGate.Release();
                }

                throw;
            }
        }
    }

    private async Task LoadEngineInternalAsync(
        SemaphoreSlim opGate,
        Action? onLoaded,
        CancellationToken ct)
    {
        try
        {
            await _catalogStore.EnsureLoadedAsync(ct).ConfigureAwait(false);

            await opGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Engine = await _engineFactory.CreateAsync(ct).ConfigureAwait(false);
                _runtimeRegistry.Clear();

                IsReady = true;
            }
            finally
            {
                opGate.Release();
            }

            onLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize torrent engine");
            throw;
        }
    }
}
