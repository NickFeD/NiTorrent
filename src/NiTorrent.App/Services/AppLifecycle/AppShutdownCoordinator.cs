using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppShutdownCoordinator : IAppShutdownCoordinator
{
    private readonly ITorrentService _torrentService;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly ILogger<AppShutdownCoordinator> _logger;

    public AppShutdownCoordinator(
        ITorrentService torrentService,
        IMainWindowLifecycle mainWindowLifecycle,
        ILogger<AppShutdownCoordinator> logger)
    {
        _torrentService = torrentService;
        _mainWindowLifecycle = mainWindowLifecycle;
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
