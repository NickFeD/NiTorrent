using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppCloseCoordinator : IAppCloseCoordinator
{
    private readonly SemaphoreSlim _exitGate = new(1, 1);
    private readonly IAppShellSettingsService _appShellSettingsService;
    private readonly ITorrentEngineMaintenanceService _torrentEngineMaintenanceService;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly ILogger<AppCloseCoordinator> _logger;

    private bool _isExiting;
    private int _closeRequestInProgress;

    public AppCloseCoordinator(
        IAppShellSettingsService appShellSettingsService,
        ITorrentEngineMaintenanceService torrentEngineMaintenanceService,
        IMainWindowLifecycle mainWindowLifecycle,
        ILogger<AppCloseCoordinator> logger)
    {
        _appShellSettingsService = appShellSettingsService;
        _torrentEngineMaintenanceService = torrentEngineMaintenanceService;
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
            var closeBehavior = _appShellSettingsService.GetCloseBehavior();
            if (closeBehavior == AppCloseBehavior.MinimizeToTray)
            {
                await MinimizeToTrayAsync().ConfigureAwait(false);
                return;
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
        => StartExitAsync(exitAsync);

    private async Task MinimizeToTrayAsync()
    {
        try
        {
            await _torrentEngineMaintenanceService.SaveAsync().ConfigureAwait(false);
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
