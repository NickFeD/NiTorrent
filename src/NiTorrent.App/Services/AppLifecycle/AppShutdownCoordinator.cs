using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppShutdownCoordinator : IAppShutdownCoordinator
{
    private readonly ITorrentEngineMaintenanceService _engineMaintenanceService;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly ILogger<AppShutdownCoordinator> _logger;

    public AppShutdownCoordinator(
        ITorrentEngineMaintenanceService engineMaintenanceService,
        IMainWindowLifecycle mainWindowLifecycle,
        ILogger<AppShutdownCoordinator> logger)
    {
        _engineMaintenanceService = engineMaintenanceService;
        _mainWindowLifecycle = mainWindowLifecycle;
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
            _mainWindowLifecycle.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Window lifecycle dispose failed");
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
            await _mainWindowLifecycle.CloseForShutdownAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exit failed while closing main window");
        }
        finally
        {
            exitApplication();
        }
    }
}
