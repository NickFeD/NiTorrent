using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppShutdownCoordinator : IAppShutdownCoordinator
{
    private readonly ITorrentService _torrentService;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly ITrayService _trayService;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<AppShutdownCoordinator> _logger;

    public AppShutdownCoordinator(
        ITorrentService torrentService,
        IMainWindowLifecycle mainWindowLifecycle,
        ITrayService trayService,
        IUiDispatcher dispatcher,
        ILogger<AppShutdownCoordinator> logger)
    {
        _torrentService = torrentService;
        _mainWindowLifecycle = mainWindowLifecycle;
        _trayService = trayService;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task ShutdownAsync(Func<Task> stopHostAsync, Action exitApplication)
    {
        try
        {
            await _torrentService.ShutdownAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Torrent service shutdown failed");
        }

        try
        {
            _trayService.SetVisible(false);
            _trayService.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tray dispose failed");
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
            try
            {
                await _dispatcher.EnqueueAsync(exitApplication).ConfigureAwait(false);
            }
            catch
            {
                exitApplication();
            }
        }
    }
}
