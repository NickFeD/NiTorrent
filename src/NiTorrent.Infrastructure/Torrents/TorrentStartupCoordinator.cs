using Microsoft.Extensions.Logging;
using MonoTorrent.Client;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentStartupCoordinator
{
    private readonly ILogger<TorrentStartupCoordinator> _logger;
    private readonly TorrentCatalogStore _catalogStore;
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly TorrentEngineFactory _engineFactory;
    private readonly TorrentStartupRecovery _startupRecovery;
    private readonly BackgroundTaskRunner _backgroundTasks;

    private Task? _initTask;

    public TorrentStartupCoordinator(
        ILogger<TorrentStartupCoordinator> logger,
        TorrentCatalogStore catalogStore,
        TorrentRuntimeRegistry runtimeRegistry,
        TorrentEngineFactory engineFactory,
        TorrentStartupRecovery startupRecovery,
        BackgroundTaskRunner backgroundTasks)
    {
        _logger = logger;
        _catalogStore = catalogStore;
        _runtimeRegistry = runtimeRegistry;
        _engineFactory = engineFactory;
        _startupRecovery = startupRecovery;
        _backgroundTasks = backgroundTasks;
    }

    public ClientEngine? Engine { get; private set; }

    public bool IsReady { get; private set; }

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
                Engine = await _engineFactory.CreateWithRecoveryAsync(ct).ConfigureAwait(false);

                var pendingRemovals = await _startupRecovery.AttachRestoredManagersAsync(
                    Engine,
                    _runtimeRegistry,
                    TorrentStableKeyAccessor.GetStableKey,
                    ct).ConfigureAwait(false);

                IsReady = true;

                _backgroundTasks.Run(
                    Task.Run(() => _startupRecovery.CompletePendingRemovalsAsync(Engine, pendingRemovals, ct), ct),
                    "complete-pending-removals");
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
