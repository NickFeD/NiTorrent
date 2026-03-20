using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using WinUIEx;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class MainWindowLifecycle : IMainWindowLifecycle, IDisposable
{
    private readonly IThemeService _themeService;
    private readonly ITrayService _trayService;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainWindowLifecycle> _logger;

    private MainWindow? _window;
    private bool _allowClose;
    private bool _trayInitialized;

    public MainWindowLifecycle(
        IThemeService themeService,
        ITrayService trayService,
        IUiDispatcher dispatcher,
        ILogger<MainWindowLifecycle> logger)
    {
        _themeService = themeService;
        _trayService = trayService;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public event Func<Task>? CloseRequested;
    public event Func<Task>? ExplicitExitRequested;

    public Window CreateAndInitialize()
    {
        if (_window is not null)
            return _window;

        _window = new MainWindow();
        _window.Title = _window.AppWindow.Title = ProcessInfoHelper.ProductNameAndVersion;
        _window.AppWindow.SetIcon("Assets/AppIcon.ico");
        _window.AppWindow.Closing += OnMainWindowClosing;

        _themeService.Initialize(_window);
        InitializeTray();

        App.MainWindow = _window;
        return _window;
    }

    public void Activate()
        => _window?.Activate();

    public Task ShowAsync()
        => _dispatcher.EnqueueAsync(() =>
        {
            EnsureWindowCreated();
            _window!.Show();
            _window.Activate();
            _trayService.SetVisible(false);
        });

    public Task HideToTrayAsync()
        => _dispatcher.EnqueueAsync(() =>
        {
            EnsureWindowCreated();
            _window!.Hide();
            _trayService.SetVisible(true);
        });

    public async Task CloseForShutdownAsync()
    {
        _allowClose = true;

        try
        {
            await _dispatcher.EnqueueAsync(() =>
            {
                _trayService.SetVisible(false);
                _window?.Close();
            }).ConfigureAwait(false);
        }
        catch
        {
            _allowClose = false;
            throw;
        }
    }

    private void InitializeTray()
    {
        if (_trayInitialized)
            return;

        _trayService.Initialize();
        _trayService.OpenRequested += OnTrayOpenRequested;
        _trayService.ExitRequested += OnTrayExitRequestedAsync;
        _trayInitialized = true;
    }

    private async void OnMainWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;

        var handler = CloseRequested;
        if (handler is null)
            return;

        try
        {
            await handler().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Main window close handler failed");
        }
    }

    private void OnTrayOpenRequested()
        => _ = ShowAsync();

    private async Task OnTrayExitRequestedAsync()
    {
        var handler = ExplicitExitRequested;
        if (handler is null)
            return;

        await handler().ConfigureAwait(false);
    }

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
            _trayService.ExitRequested -= OnTrayExitRequestedAsync;
            _trayService.SetVisible(false);
            _trayInitialized = false;
        }

        if (_window is not null)
            _window.AppWindow.Closing -= OnMainWindowClosing;
    }
}
