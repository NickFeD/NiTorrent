using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppShutdownCoordinator : IAppShutdownCoordinator
{
    private readonly ITorrentEngineMaintenanceService _engineMaintenanceService;
    private readonly ITorrentEngineLifecycle _engineLifecycle;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly ILogger<AppShutdownCoordinator> _logger;
    private readonly IUiDispatcher _dispatcher;

    public AppShutdownCoordinator(
        ITorrentEngineMaintenanceService engineMaintenanceService,
        ITorrentEngineLifecycle engineLifecycle,
        IMainWindowLifecycle mainWindowLifecycle,
        IUiDispatcher dispatcher,
        ILogger<AppShutdownCoordinator> logger)
    {
        _engineMaintenanceService = engineMaintenanceService;
        _engineLifecycle = engineLifecycle;
        _mainWindowLifecycle = mainWindowLifecycle;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task ShutdownAsync(Func<Task> stopHostAsync, Action exitApplication)
    {
        try
        {
            await _engineMaintenanceService.ShutdownAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Torrent service shutdown failed");
        }

        try
        {
            await _engineLifecycle.ShutdownAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Torrent engine lifecycle shutdown failed");
        }

        try
        {
            await _mainWindowLifecycle.CloseForShutdownAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exit failed while closing main window");
        }

        try
        {
            await stopHostAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Host stop failed");
        }

        try
        {
            _mainWindowLifecycle.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Window lifecycle dispose failed");
        }
        finally
        {
            await _dispatcher.EnqueueAsync(exitApplication).ConfigureAwait(false);
        }
    }
}
