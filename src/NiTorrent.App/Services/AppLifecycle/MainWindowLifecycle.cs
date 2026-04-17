using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using NiTorrent.App.Views;
using NiTorrent.Application.Settings;
using NiTorrent.Domain.Settings;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using WinUIEx;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed partial class MainWindowLifecycle(
    IThemeService themeService,
    ITrayService trayService,
    IUiDispatcher dispatcher,
    AppSettingsService settingsService,
     NiTorrent.Application.AppCloseCoordinator closeCoordinator) : IDisposable
{
    private readonly IThemeService _themeService = themeService;
    private readonly ITrayService _trayService = trayService;
    private readonly IUiDispatcher _dispatcher = dispatcher;
    private readonly AppSettingsService _settingsService = settingsService;

    private readonly  NiTorrent.Application.AppCloseCoordinator _closeCoordinator = closeCoordinator;

    private MainWindow _window;
    private AppCloseBehavior _closeBehavior; 
    private bool _allowClose = false;
    private bool _trayInitialized;

    public Window CreateAndInitialize()
    {
        _closeBehavior = _settingsService.Current.CloseBehavior;
        _settingsService.Changed += OnSettingsChanged;
        if (_window is not null)
            return _window;

        var window = new MainWindow();
        window.Title = window.AppWindow.Title = ProcessInfoHelper.ProductNameAndVersion;
        window.AppWindow.SetIcon("Assets/AppIcon.ico");
        window.AppWindow.Closing += OnMainWindowClosing;

        _themeService.Initialize(window);
        InitializeTray();

        _window = window;
        App.MainWindow = window;

        return window;
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _closeBehavior = settings.CloseBehavior;
    }

    public void Activate()
        => _window?.Activate();

    public Task ShowAsync()
        => _dispatcher.EnqueueAsync(() =>
        {
            EnsureWindowCreated();
            var window = _window!;
            _trayService.SetVisible(false);
            window.Show();
            window.Activate();
        });

    public Task HideToTrayAsync()
        => _dispatcher.EnqueueAsync(() =>
        {
            EnsureWindowCreated();
            var window = _window!;
            window.Hide();
            _trayService.SetVisible(true);
        });

    public Task CloseForShutdownAsync()
        => _dispatcher.EnqueueAsync(() =>
        {
            EnsureWindowCreated();
            var window = _window!;

            _trayService.SetVisible(false);

            // Explicit shutdown must bypass regular close-to-tray interception.
            _allowClose = true;
            window.Close();
        });

    private void InitializeTray()
    {
        if (_trayInitialized)
            return;

        _trayService.Initialize();
        _trayService.OpenRequested += OnTrayOpenRequested;
        _trayService.ExitRequested += CloseForShutdownAsync;
        _trayInitialized = true;
    }

    private async void OnMainWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs e)
    {
        if (_allowClose)
        {
            _closeCoordinator.Closing();
            return;
        }

        switch (_closeBehavior)
        {
            case AppCloseBehavior.ExitApplication:
                _closeCoordinator.Closing();
                return;
            case AppCloseBehavior.MinimizeToTray:
                e.Cancel = true;
                await HideToTrayAsync();
                break;
            case AppCloseBehavior.AskUser:
                break;
            default:
                break;
        }
    }

    private void OnTrayOpenRequested()
        => _ = ShowAsync();
    private void EnsureWindowCreated()
    {
        if (_window is null)
            throw new InvalidOperationException("Main window is not initialized");
    }

    public void Dispose()
    {
        if (_trayInitialized)
        {
            _trayService.OpenRequested -= OnTrayOpenRequested;
            _trayService.ExitRequested -= CloseForShutdownAsync;
            _trayInitialized = false;
        }

        _trayService.Dispose();

        _window?.AppWindow.Closing -= OnMainWindowClosing;
    }
}
