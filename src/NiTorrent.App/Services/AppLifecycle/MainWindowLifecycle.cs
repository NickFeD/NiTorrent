using NiTorrent.Application;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Settings.Enums;
using NiTorrent.Presentation.Abstractions;
using WinUIEx;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed partial class MainWindowLifecycle(
    IThemeService themeService,
    ITrayService trayService,
    IUiDispatcher dispatcher,
    AppSettingsService settingsService,
    IAppShutdownService shutdownService) : IDisposable, IAppShellLifecycle
{
    private readonly IThemeService _themeService = themeService;
    private readonly ITrayService _trayService = trayService;
    private readonly IUiDispatcher _dispatcher = dispatcher;
    private readonly AppSettingsService _settingsService = settingsService;
    private readonly IAppShutdownService _shutdownService = shutdownService;

    private MainWindow? _window;
    private AppCloseBehavior _closeBehavior;
    private bool _allowClose;
    private bool _trayInitialized;
    private bool _windowClosingHandlerAttached;
    private bool _disposed;

    public Window? CurrentWindow => _window;

    public Task StartAsync(CancellationToken ct = default)
        => _dispatcher.EnqueueAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            CreateAndInitialize();
            Activate();
        });

    private void OnSettingsChanged(AppSettings settings)
        => _closeBehavior = settings.CloseBehavior;

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

    public Task CloseAsync()
        => _dispatcher.EnqueueAsync(() =>
        {
            var window = _window;
            if (window is null)
                return;

            _trayService.SetVisible(false);
            _allowClose = true;
            DetachWindowClosingHandler();
            window.Close();
        });

    public Task OpenTorrentFileAsync(string filePath)
        => _dispatcher.EnqueueAsync(() =>
        {
            EnsureWindowCreated();
            var window = _window!;
            _trayService.SetVisible(false);
            window.Show();
            window.Activate();
            window.OpenTorrentFileFromActivation(filePath);
        });


    public Task OpenMagnetLinkAsync(string magnetLink)
        => _dispatcher.EnqueueAsync(() =>
        {
            EnsureWindowCreated();

            var window = _window!;

            _trayService.SetVisible(false);
            window.Show();
            window.Activate();

            window.OpenMagnetLinkFromActivation(magnetLink);
        });

    private void InitializeTray()
    {
        if (_trayInitialized)
            return;

        _trayService.Initialize();
        _trayService.OpenRequested += OnTrayOpenRequested;
        _trayService.ExitRequested += OnTrayExitRequested;
        _trayInitialized = true;
    }

    private async void OnMainWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs e)
    {
        if (_allowClose)
            return;

        switch (_closeBehavior)
        {
            case AppCloseBehavior.ExitApplication:
                e.Cancel = true;
                _shutdownService.RequestShutdown();
                return;
            case AppCloseBehavior.MinimizeToTray:
                e.Cancel = true;
                await HideToTrayAsync();
                break;
            case AppCloseBehavior.AskUser:
                e.Cancel = true;
                _shutdownService.RequestShutdown();
                break;
        }
    }

    private void OnTrayOpenRequested()
        => _ = ShowAsync();

    private Task OnTrayExitRequested()
    {
        _shutdownService.RequestShutdown();
        return Task.CompletedTask;
    }

    private Window CreateAndInitialize()
    {
        if (_window is not null)
            return _window;

        _closeBehavior = _settingsService.Current.CloseBehavior;
        _settingsService.Changed += OnSettingsChanged;

        var window = new MainWindow();
        window.Title = window.AppWindow.Title = ProcessInfoHelper.ProductNameAndVersion;
        window.AppWindow.SetIcon("Assets/AppIcon.ico");
        window.AppWindow.Closing += OnMainWindowClosing;
        _windowClosingHandlerAttached = true;

        _themeService.Initialize(window);
        InitializeTray();

        _window = window;
        return window;
    }

    private void EnsureWindowCreated()
    {
        if (_window is null)
            throw new InvalidOperationException("Main window is not initialized");
    }

    private void DetachWindowClosingHandler()
    {
        if (!_windowClosingHandlerAttached)
            return;

        var appWindow = _window?.AppWindow;
        if (appWindow is not null)
            appWindow.Closing -= OnMainWindowClosing;

        _windowClosingHandlerAttached = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _settingsService.Changed -= OnSettingsChanged;

        if (_trayInitialized)
        {
            _trayService.OpenRequested -= OnTrayOpenRequested;
            _trayService.ExitRequested -= OnTrayExitRequested;
            _trayInitialized = false;
        }

        DetachWindowClosingHandler();
        _trayService.Dispose();
    }
}
