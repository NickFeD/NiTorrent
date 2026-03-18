using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.App.Services;
using NiTorrent.Application.Abstractions;
using NiTorrent.Infrastructure.DI;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using WinUIEx;
using WinUIApplication = Microsoft.UI.Xaml.Application;

namespace NiTorrent.App;

public partial class App : WinUIApplication
{
    private readonly IHost _host;
    private readonly SemaphoreSlim _exitGate = new(1, 1);
    private Task? _hostStartTask;
    private Task? _engineInitTask;
    private bool _isExiting;
    private int _closeRequestInProgress;

    public new static App Current => (App)WinUIApplication.Current;
    public static Window MainWindow = Window.Current;
    public static IntPtr Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
    public IServiceProvider Services { get; }
    public IJsonNavigationService NavService => GetService<IJsonNavigationService>();

    public static T GetService<T>() where T : class
    {
        if ((App.Current as App)!.Services.GetService(typeof(T)) is not T service)
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");

        return service;
    }

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices(ConfigureServices)
            .Build();

        Services = _host.Services;
        InitializeComponent();
    }

    private static void ConfigureServices(HostBuilderContext contexts, IServiceCollection services)
    {
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ContextMenuService>();
        services.AddSingleton<IAppStorageService, AppStorageService>();

        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NiTorrent",
            "Logs",
            $"app-{DateTime.Now:yyyyMMdd}.log");

        services.AddNiTorrentInfrastructure();
        services.AddNiTorrentPresentation();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(logsPath));
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<UiDispatcherHolder>();
        services.AddSingleton<IUiDispatcher>(sp =>
        {
            var holder = sp.GetRequiredService<UiDispatcherHolder>();
            return new WinUiDispatcher(holder.Queue ?? throw new InvalidOperationException("UI Dispatcher not initialized"));
        });
        services.AddSingleton<IAppInfo, DevWinAppInfo>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IUriLauncher, WinUriLauncher>();
        services.AddSingleton<IPickerHelper, WinPickerHelper>();
        services.AddSingleton<IDialogService, WinUiDialogService>();
        services.AddSingleton<IUpdateService, DevWinUiUpdateService>();
        services.AddSingleton<IJsonNavigationService, JsonNavigationService>();
        services.AddSingleton<ITorrentPreviewDialogService, TorrentPreviewDialogService>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainInstance = AppInstance.FindOrRegisterForKey("main");
        if (!mainInstance.IsCurrent)
        {
            await mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit();
            return;
        }

        mainInstance.Activated += (_, e) => _ = HandleActivationSafeAsync(e);

        MainWindow = new MainWindow();
        MainWindow.Title = MainWindow.AppWindow.Title = ProcessInfoHelper.ProductNameAndVersion;
        MainWindow.AppWindow.SetIcon("Assets/AppIcon.ico");
        MainWindow.AppWindow.Closing += OnMainWindowClosing;

        var holder = GetService<UiDispatcherHolder>();
        holder.Initialize(DispatcherQueue.GetForCurrentThread());

        GetService<IThemeService>().Initialize(MainWindow);

        var tray = GetService<ITrayService>();
        tray.Initialize();
        tray.OpenRequested += ShowMainWindow;
        tray.ExitRequested += ExitAsync;

        MainWindow.Activate();

        StartBackgroundInitialization();
        _ = HandleActivationSafeAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
    }

    private void StartBackgroundInitialization()
    {
        _hostStartTask ??= StartHostAndShellAsync();
        _engineInitTask ??= InitializeTorrentEngineSafeAsync();
    }

    private async Task StartHostAndShellAsync()
    {
        try
        {
            await _host.StartAsync().ConfigureAwait(false);

            var menuService = GetService<ContextMenuService>();
            if (menuService is not null && RuntimeHelper.IsPackaged())
            {
                var menu = new ContextMenuItem
                {
                    Title = "Open NiTorrent.App Here",
                    Param = @"""{path}""",
                    AcceptFileFlag = (int)FileMatchFlagEnum.All,
                    AcceptDirectoryFlag = (int)(DirectoryMatchFlagEnum.Directory | DirectoryMatchFlagEnum.Background | DirectoryMatchFlagEnum.Desktop),
                    AcceptMultipleFilesFlag = (int)FilesMatchFlagEnum.Each,
                    Index = 0,
                    Enabled = true,
                    Icon = ProcessInfoHelper.GetFileVersionInfo().FileName,
                    Exe = "NiTorrent.App.exe"
                };

                await menuService.SaveAsync(menu).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _host.Services.GetService<ILogger<App>>()?.LogError(ex, "Application background initialization failed");
        }
    }

    private async Task InitializeTorrentEngineSafeAsync()
    {
        try
        {
            await GetService<ITorrentService>().InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _host.Services.GetService<ILogger<App>>()?.LogError(ex, "Torrent engine initialization failed");
        }
    }

    private void OnMainWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs e)
    {
        if (_isExiting)
            return;

        e.Cancel = true;

        if (Interlocked.Exchange(ref _closeRequestInProgress, 1) == 1)
            return;

        _ = HandleCloseRequestAsync();
    }

    private async Task HandleCloseRequestAsync()
    {
        try
        {
            var prefs = GetService<ITorrentPreferences>();
            var logger = _host.Services.GetService<ILogger<App>>();

            WindowCloseAction action;
            if (prefs.ShowCloseActionDialogOnClose)
            {
                var choice = await GetService<IDialogService>()
                    .ShowWindowCloseChoiceAsync(defaultMinimizeToTray: prefs.MinimizeToTrayOnClose)
                    .ConfigureAwait(false);

                if (choice is null)
                    return;

                action = choice.Action;

                if (choice.RememberChoice)
                {
                    prefs.ShowCloseActionDialogOnClose = false;
                    prefs.MinimizeToTrayOnClose = action == WindowCloseAction.MinimizeToTray;
                }
            }
            else
            {
                action = prefs.MinimizeToTrayOnClose
                    ? WindowCloseAction.MinimizeToTray
                    : WindowCloseAction.ExitApplication;
            }

            if (action == WindowCloseAction.MinimizeToTray)
            {
                try
                {
                    await GetService<ITorrentService>().SaveAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to save state before minimizing to tray");
                }

                await GetService<IUiDispatcher>().EnqueueAsync(() =>
                {
                    MainWindow.Hide();
                    GetService<ITrayService>().SetVisible(true);
                }).ConfigureAwait(false);

                return;
            }

            await ExitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _host.Services.GetService<ILogger<App>>()?.LogError(ex, "Failed to process window close request");
        }
        finally
        {
            Interlocked.Exchange(ref _closeRequestInProgress, 0);
        }
    }

    private async Task HandleActivationSafeAsync(AppActivationArguments args)
    {
        try
        {
            await HandleActivationCoreAsync(args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var logger = _host.Services.GetService<ILogger<App>>();
            logger?.LogError(ex, "File activation handling failed");

            try
            {
                await GetService<IDialogService>().ShowTextAsync("Не удалось открыть торрент-файл", ex.Message).ConfigureAwait(false);
            }
            catch (Exception dialogEx)
            {
                logger?.LogWarning(dialogEx, "Failed to show file activation error dialog");
            }
        }
    }

    private async Task HandleActivationCoreAsync(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.File || args.Data is not FileActivatedEventArgs fileArgs)
            return;

        StartBackgroundInitialization();
        ShowMainWindow();

        var torrentPreviewDialog = GetService<ITorrentPreviewDialogService>();
        var torrentService = GetService<ITorrentService>();

        foreach (var item in fileArgs.Files)
        {
            if (item is not StorageFile file || !file.FileType.Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                continue;

            var preview = await torrentService.GetPreviewAsync(new NiTorrent.Application.Torrents.TorrentSource.TorrentFile(file.Path)).ConfigureAwait(false);
            var dialogResult = await torrentPreviewDialog.ShowAsync(preview).ConfigureAwait(false);
            if (dialogResult is null)
                continue;

            await torrentService.AddAsync(new(
                new NiTorrent.Application.Torrents.TorrentSource.TorrentFile(file.Path),
                dialogResult.OutputFolder,
                dialogResult.SelectedFilePaths.ToHashSet())).ConfigureAwait(false);
        }
    }

    private void ShowMainWindow()
    {
        _ = GetService<IUiDispatcher>().EnqueueAsync(() =>
        {
            MainWindow.Show();
            MainWindow.Activate();
        });
    }

    private async Task ExitAsync()
    {
        await _exitGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isExiting)
                return;

            _isExiting = true;

            var logger = _host.Services.GetService<ILogger<App>>();

            try
            {
                await GetService<ITorrentService>().ShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Torrent service shutdown failed");
            }

            try
            {
                GetService<TrayService>().Dispose();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Tray dispose failed");
            }

            try
            {
                if (_hostStartTask is not null)
                    await _hostStartTask.ConfigureAwait(false);

                await _host.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Host stop failed");
            }

            await GetService<IUiDispatcher>().EnqueueAsync(() => MainWindow?.Close()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _host.Services.GetService<ILogger<App>>()?.LogError(ex, "Exit failed");
        }
        finally
        {
            _exitGate.Release();
            Exit();
        }
    }
}
