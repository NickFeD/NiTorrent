using NiTorrent.Torrents.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using NiTorrent.Application;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Settings.Enums;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Tray;
using NiTorrent.Presentation;
using WinUIEx;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed partial class MainWindowLifecycle(IThemeService themeService, ITrayService trayService,
    IUiDispatcher dispatcher, ITorrentSettings settingsService, IAppShutdownService shutdownService,
    ILogger<MainWindowLifecycle> logger, DesktopObservationLifetime observationLifetime) : IDisposable, IAppShellLifecycle
{
    private readonly TrayLifecycle _tray = new(trayService);
    private MainWindow? _window;
    private ContentDialog? _dialog;
    private bool _subscribed;
    private bool _stopping;
    private bool _closePending;
    private bool _disposed;
    private int _activationVersion;
    public Window? CurrentWindow => _window;

    public Task StartAsync(CancellationToken ct = default) => dispatcher.EnqueueAsync(() =>
    {
        ct.ThrowIfCancellationRequested();
        CreateAndInitialize();
        ShowWindow();
        logger.LogInformation("[Lifecycle] Shell shown");
    }, ct);

    public void Activate() { if (!_stopping) ShowWindow(); }
    public Task ShowAsync() => dispatcher.EnqueueAsync(() =>
    {
        if (_stopping) return;
        CreateAndInitialize();
        ShowWindow();
    });

    private void ShowWindow()
    {
        if (_stopping || _window is null) return;
        _activationVersion++;
        _dialog?.Hide();
        _window.Show();
        if (_window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        _window.Activate();
        observationLifetime.SetVisible(true);
        _tray.Leave();
    }

    public Task HideToTrayAsync() => OnUiAsync(EnterTrayAsync);
    private async Task EnterTrayAsync()
    {
        if (_stopping || _window is null) return;
        var result = await _tray.EnterAsync(() => _window.Hide());
        if (result == TrayTransitionResult.Entered) observationLifetime.SetVisible(false);
        if (result != TrayTransitionResult.Failed || _stopping) return;
        logger.LogWarning(_tray.LastError, "[Tray] Both registration attempts failed");
        ShowWindow();
        await ShowTrayErrorAsync();
    }

    public Task CloseAsync() => dispatcher.EnqueueAsync(() =>
    {
        if (_stopping) return;
        _stopping = true;
        observationLifetime.SetVisible(false);
        _activationVersion++;
        _dialog?.Hide();
        _tray.Dispose();
        StopTray();
        if (_window is not null)
        {
            _window.AppWindow.Closing -= OnMainWindowClosing;
            _window.Hide();
        }
    });

    public Task OpenTorrentFileAsync(string filePath) => dispatcher.EnqueueAsync(() =>
    {
        if (_stopping) return;
        CreateAndInitialize();
        ShowWindow();
        _window!.OpenTorrentFileFromActivation(filePath);
    });
    public Task OpenMagnetLinkAsync(string magnetLink) => dispatcher.EnqueueAsync(() =>
    {
        if (_stopping) return;
        CreateAndInitialize();
        ShowWindow();
        _window!.OpenMagnetLinkFromActivation(magnetLink);
    });

    // Subscribe at startup, but allocate/register a native icon only when entering the tray.
    public Task StartTrayAsync(CancellationToken ct = default) => dispatcher.EnqueueAsync(() =>
    {
        if (_subscribed || _stopping) return;
        trayService.OpenRequested += OnTrayOpenRequested;
        trayService.ExitRequested += OnTrayExitRequested;
        trayService.RecoveryRequested += OnRecoveryRequested;
        trayService.BulkFailed += OnBulkFailed;
        _subscribed = true;
    }, ct);
    public Task StopTrayAsync(CancellationToken ct = default) => dispatcher.EnqueueAsync(StopTray, ct);
    private void StopTray()
    {
        if (_subscribed)
        {
            trayService.OpenRequested -= OnTrayOpenRequested;
            trayService.ExitRequested -= OnTrayExitRequested;
            trayService.RecoveryRequested -= OnRecoveryRequested;
            trayService.BulkFailed -= OnBulkFailed;
            _subscribed = false;
        }
        trayService.Dispose();
    }

    private async void OnMainWindowClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (_stopping) return;
        e.Cancel = true;
        if (_closePending || _dialog is not null) return;
        _closePending = true;
        try
        {
            switch (settingsService.Current.CloseBehavior)
            {
                case AppCloseBehavior.ExitApplication:
                    shutdownService.RequestShutdown();
                    break;
                case AppCloseBehavior.MinimizeToTray:
                    await EnterTrayAsync();
                    break;
                default:
                    await AskCloseAsync();
                    break;
            }
        }
        catch (Exception ex) { logger.LogError(ex, "[Tray] Close request failed; window remains available"); ShowWindow(); }
        finally { _closePending = false; }
    }

    private async Task AskCloseAsync()
    {
        var remember = new CheckBox { Content = "Запомнить выбор", Margin = new Thickness(0, 12, 0, 0) };
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock { Text = "Завершить приложение или продолжить работу в трее?", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(remember);
        var version = _activationVersion;
        var result = await ShowDialogAsync(new ContentDialog
        {
            Title = "Закрытие NiTorrent", Content = content,
            PrimaryButtonText = "Завершить приложение", SecondaryButtonText = "Свернуть в трей",
            CloseButtonText = "Отмена", DefaultButton = ContentDialogButton.Close
        });
        if (_stopping || version != _activationVersion || result == ContentDialogResult.None) return;
        var behavior = result == ContentDialogResult.Primary ? AppCloseBehavior.ExitApplication : AppCloseBehavior.MinimizeToTray;
        if (remember.IsChecked == true)
            await settingsService.SaveAsync(new AppSettings { EngineSettings = settingsService.Current.EngineSettings, CloseBehavior = behavior });
        if (_stopping || version != _activationVersion) return;
        if (behavior == AppCloseBehavior.ExitApplication) shutdownService.RequestShutdown();
        else await EnterTrayAsync();
    }

    private async Task ShowTrayErrorAsync()
    {
        var result = await ShowDialogAsync(new ContentDialog
        {
            Title = "Не удалось создать значок в трее",
            Content = "Две попытки не удались. Можно завершить NiTorrent или оставить окно открытым.",
            PrimaryButtonText = "Завершить приложение", CloseButtonText = "Оставить открытым",
            DefaultButton = ContentDialogButton.Close
        });
        if (!_stopping && result == ContentDialogResult.Primary) shutdownService.RequestShutdown();
    }

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_stopping || _dialog is not null || _window?.Content?.XamlRoot is not { } root)
            return ContentDialogResult.None;
        _dialog = dialog;
        dialog.XamlRoot = root;
        try { return await dialog.ShowAsync(); }
        finally { _dialog = null; }
    }

    private void OnTrayOpenRequested() => _ = ObserveAsync(ShowAsync());
    private Task OnTrayExitRequested() { shutdownService.RequestShutdown(); return Task.CompletedTask; }
    private void OnRecoveryRequested()
    {
        if (_tray.IsInTray && !_stopping) _ = ObserveAsync(OnUiAsync(EnterTrayAsync));
    }
    private void OnBulkFailed(int failed) => _ = ObserveAsync(OnUiAsync(async () =>
    {
        if (_stopping) return;
        ShowWindow();
        await ShowDialogAsync(new ContentDialog
        {
            Title = "Не удалось обработать все торренты",
            Content = failed < 0 ? "Команда не выполнена. Подробности записаны в журнал." : $"Не удалось изменить состояние {failed} торрентов. Остальные записи обработаны.",
            CloseButtonText = "Закрыть"
        });
    }));
    private async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch (Exception ex) { logger.LogError(ex, "[Tray] UI operation failed"); }
    }
    private Task OnUiAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(async () =>
        {
            try { await action(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        })) completion.TrySetException(new InvalidOperationException("UI dispatcher is unavailable."));
        return completion.Task;
    }

    private void CreateAndInitialize()
    {
        if (_window is not null || _stopping) return;
        var window = new MainWindow();
        window.Title = window.AppWindow.Title = ProcessInfoHelper.ProductNameAndVersion;
        window.AppWindow.SetIcon("Assets/AppIcon.ico");
        window.AppWindow.Closing += OnMainWindowClosing;
        themeService.Initialize(window);
        _window = window;
        logger.LogInformation("[Lifecycle] Shell created");
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopping = true;
        _dialog?.Hide();
        _tray.Dispose();
        StopTray();
        if (_window is not null) _window.AppWindow.Closing -= OnMainWindowClosing;
    }
}
