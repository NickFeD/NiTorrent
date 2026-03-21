using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Settings;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppCloseCoordinator : IAppCloseCoordinator
{
    private readonly SemaphoreSlim _exitGate = new(1, 1);
    private readonly IAppShellSettingsService _shellSettings;
    private readonly ITorrentEngineMaintenanceService _engineMaintenanceService;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly ILogger<AppCloseCoordinator> _logger;

    private bool _isExiting;
    private int _closeRequestInProgress;

    public AppCloseCoordinator(
        IAppShellSettingsService shellSettings,
        ITorrentEngineMaintenanceService engineMaintenanceService,
        IMainWindowLifecycle mainWindowLifecycle,
        ILogger<AppCloseCoordinator> logger)
    {
        _shellSettings = shellSettings;
        _engineMaintenanceService = engineMaintenanceService;
        _mainWindowLifecycle = mainWindowLifecycle;
        _logger = logger;
    }

    public bool IsExitInProgress => _isExiting;

    public async Task RequestCloseFromWindowAsync(Func<Task> exitAsync)
    {
        if (_isExiting)
            return;

        if (Interlocked.Exchange(ref _closeRequestInProgress, 1) == 1)
            return;

        try
        {
            var action = AppShellClosePolicy.Resolve(
                _shellSettings.GetCloseBehavior(),
                AppShellCloseRequestSource.MainWindow);

            switch (action)
            {
                case AppShellCloseAction.MinimizeToTray:
                    await MinimizeToTrayAsync().ConfigureAwait(false);
                    return;
                case AppShellCloseAction.AskUser:
                    _logger.LogInformation("AskUser close behavior is not implemented yet. Falling back to exit.");
                    break;
                case AppShellCloseAction.ExitApplication:
                default:
                    break;
            }

            await StartExitAsync(exitAsync).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process window close request");
        }
        finally
        {
            Interlocked.Exchange(ref _closeRequestInProgress, 0);
        }
    }

    public Task RequestExplicitExitAsync(Func<Task> exitAsync)
    {
        var action = AppShellClosePolicy.Resolve(
            _shellSettings.GetCloseBehavior(),
            AppShellCloseRequestSource.TrayExit);

        return action switch
        {
            AppShellCloseAction.MinimizeToTray => MinimizeToTrayAsync(),
            _ => StartExitAsync(exitAsync),
        };
    }

    private async Task MinimizeToTrayAsync()
    {
        try
        {
            await _engineMaintenanceService.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save state before minimizing to tray");
        }

        await _mainWindowLifecycle.HideToTrayAsync().ConfigureAwait(false);
    }

    private async Task StartExitAsync(Func<Task> exitAsync)
    {
        await _exitGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isExiting)
                return;

            _isExiting = true;
            await exitAsync().ConfigureAwait(false);
        }
        finally
        {
            _exitGate.Release();
        }
    }
}
