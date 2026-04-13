using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Shell;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Settings;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

// HACK: нужно будет сделать систему координации закрытия приложения более гибкой, чтобы не завязываться на конкретные сценарии и юзать ее для разных частей приложения, а не только для главного окна и трея также сохронять при закрытии не только состояние движка, но и состояние приложения в целом, чтобы при открытии восстанавливать его в том же состоянии

public sealed class AppCloseCoordinator : IAppCloseCoordinator
{
    private readonly SemaphoreSlim _exitGate = new(1, 1);
    private readonly SemaphoreSlim _closeGate = new(1, 1);
    private readonly HandleWindowCloseWorkflow _handleWindowCloseWorkflow;
    private readonly HandleTrayExitWorkflow _handleTrayExitWorkflow;
    private readonly ITorrentEngineMaintenanceService _engineMaintenanceService;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AppCloseCoordinator> _logger;

    private bool _isExiting;
    private int _pendingWindowCloseRequest;

    public AppCloseCoordinator(
        HandleWindowCloseWorkflow handleWindowCloseWorkflow,
        HandleTrayExitWorkflow handleTrayExitWorkflow,
        ITorrentEngineMaintenanceService engineMaintenanceService,
        IMainWindowLifecycle mainWindowLifecycle,
        IDialogService dialogService,
        ILogger<AppCloseCoordinator> logger)
    {
        _handleWindowCloseWorkflow = handleWindowCloseWorkflow;
        _handleTrayExitWorkflow = handleTrayExitWorkflow;
        _engineMaintenanceService = engineMaintenanceService;
        _mainWindowLifecycle = mainWindowLifecycle;
        _dialogService = dialogService;
        _logger = logger;
    }

    public bool IsExitInProgress => _isExiting;

    public async Task RequestCloseFromWindowAsync(Func<Task> exitAsync)
    {
        if (_isExiting)
            return;

        Interlocked.Exchange(ref _pendingWindowCloseRequest, 1);

        await _closeGate.WaitAsync().ConfigureAwait(false);

        try
        {
            while (Interlocked.Exchange(ref _pendingWindowCloseRequest, 0) == 1)
            {
                if (_isExiting)
                    return;

                var action = await _handleWindowCloseWorkflow.ExecuteAsync().ConfigureAwait(false);

                switch (action)
                {
                    case AppShellCloseAction.MinimizeToTray:
                        await MinimizeToTrayAsync().ConfigureAwait(false);
                        break;
                    case AppShellCloseAction.AskUser:
                        var choice = await _dialogService
                            .ShowWindowCloseChoiceAsync(defaultMinimizeToTray: false)
                            .ConfigureAwait(false);

                        if (choice is null)
                            return;

                        if (choice.RememberChoice)
                        {
                            //var settings = await _settingsRepository.LoadAsync().ConfigureAwait(false);
                            //var updated = settings with
                            //{
                            //    CloseBehavior = choice.Action == WindowCloseAction.MinimizeToTray
                            //        ? AppCloseBehavior.MinimizeToTray
                            //        : AppCloseBehavior.ExitApplication
                            //};
                            //await _settingsRepository.SaveAsync(updated).ConfigureAwait(false);
                        }

                        if (choice.Action == WindowCloseAction.MinimizeToTray)
                        {
                            await MinimizeToTrayAsync().ConfigureAwait(false);
                            break;
                        }

                        await StartExitAsync(exitAsync).ConfigureAwait(false);
                        return;
                    case AppShellCloseAction.ExitApplication:
                    default:
                        await StartExitAsync(exitAsync).ConfigureAwait(false);
                        return;
                }

                // Continue loop if a newer close request arrived while handling this one.
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process window close request");
        }
        finally
        {
            _closeGate.Release();
        }
    }

    public Task RequestExplicitExitAsync(Func<Task> exitAsync)
    {
        var action = _handleTrayExitWorkflow.Execute();

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
            try
            {
                await exitAsync().ConfigureAwait(false);
            }
            finally
            {
                _isExiting = false;
            }
        }
        finally
        {
            _exitGate.Release();
        }
    }
}
