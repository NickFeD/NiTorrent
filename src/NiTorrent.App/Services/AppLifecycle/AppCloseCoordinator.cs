using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppCloseCoordinator : IAppCloseCoordinator
{
    private readonly SemaphoreSlim _exitGate = new(1, 1);
    private readonly ITorrentPreferences _preferences;
    private readonly ITorrentService _torrentService;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly ILogger<AppCloseCoordinator> _logger;

    private bool _isExiting;
    private int _closeRequestInProgress;

    public AppCloseCoordinator(
        ITorrentPreferences preferences,
        ITorrentService torrentService,
        IMainWindowLifecycle mainWindowLifecycle,
        ILogger<AppCloseCoordinator> logger)
    {
        _preferences = preferences;
        _torrentService = torrentService;
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
            if (_preferences.MinimizeToTrayOnClose)
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
            await _torrentService.SaveAsync().ConfigureAwait(false);
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
