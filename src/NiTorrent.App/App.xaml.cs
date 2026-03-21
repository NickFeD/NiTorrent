using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.App.Services;
using NiTorrent.App.Services.AppLifecycle;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Shell;
using NiTorrent.Infrastructure.DI;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using WinUIEx;
using Windows.ApplicationModel.Activation;
using WinUIApplication = Microsoft.UI.Xaml.Application;

namespace NiTorrent.App;

public partial class App : WinUIApplication
{
    private readonly IHost _host;
    private Task? _hostStartTask;
    private Task? _engineInitTask;

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
        services.AddSingleton<IFolderLauncher, FolderLauncher>();
        services.AddSingleton<IPickerHelper, WinPickerHelper>();
        services.AddSingleton<IDialogService, WinUiDialogService>();
        services.AddSingleton<IUpdateService, DevWinUiUpdateService>();
        services.AddSingleton<IJsonNavigationService, JsonNavigationService>();
        services.AddSingleton<ITorrentPreviewDialogService, TorrentPreviewDialogService>();
        services.AddSingleton<IAppStartupService, AppStartupService>();
        services.AddSingleton<IAppActivationService, AppActivationService>();
        services.AddSingleton<IMainWindowLifecycle, MainWindowLifecycle>();
        services.AddSingleton<IAppShellSettingsService, AppShellSettingsService>();
        services.AddSingleton<IAppCloseCoordinator, AppCloseCoordinator>();
        services.AddSingleton<IAppShutdownCoordinator, AppShutdownCoordinator>();
        services.AddTransient<AddTorrentUseCase>();
        services.AddTransient<ITorrentPreviewFlow, TorrentPreviewFlow>();
        services.AddTransient<PickAndAddTorrentUseCase>();
        services.AddTransient<AddTorrentFileWithPreviewUseCase>();
        services.AddTransient<AddMagnetUseCase>();
        services.AddTransient<StartTorrentUseCase>();
        services.AddTransient<PauseTorrentUseCase>();
        services.AddTransient<RemoveTorrentUseCase>();
        services.AddTransient<OpenTorrentFolderUseCase>();
        services.AddTransient<ApplyTorrentSettingsUseCase>();
        services.AddTransient<ITorrentWorkflowService, TorrentWorkflowService>();
    }

    protected override async void OnLaunched( Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var mainInstance = AppInstance.FindOrRegisterForKey("main");
        if (!mainInstance.IsCurrent)
        {
            await mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit();
            return;
        }

        mainInstance.Activated += (_, e) => _ = GetService<IAppActivationService>()
            .HandleAsync(e, ShowMainWindow, StartBackgroundInitialization);

        var holder = GetService<UiDispatcherHolder>();
        holder.Initialize(DispatcherQueue.GetForCurrentThread());

        var mainWindowLifecycle = GetService<IMainWindowLifecycle>();
        mainWindowLifecycle.CloseRequested += OnMainWindowCloseRequestedAsync;
        mainWindowLifecycle.ExplicitExitRequested += OnExplicitExitRequestedAsync;

        MainWindow = mainWindowLifecycle.CreateAndInitialize();
        mainWindowLifecycle.Activate();

        StartBackgroundInitialization();
        _ = GetService<IAppActivationService>()
            .HandleAsync(AppInstance.GetCurrent().GetActivatedEventArgs(), ShowMainWindow, StartBackgroundInitialization);
    }

    private void StartBackgroundInitialization()
    {
        var startup = GetService<IAppStartupService>();
        _hostStartTask ??= startup.StartHostAndShellAsync(_host);
        _engineInitTask ??= startup.InitializeTorrentEngineAsync();
    }

    private void ShowMainWindow()
        => _ = GetService<IMainWindowLifecycle>().ShowAsync();

    private Task OnMainWindowCloseRequestedAsync()
        => GetService<IAppCloseCoordinator>().RequestCloseFromWindowAsync(ExitApplicationAsync);

    private Task OnExplicitExitRequestedAsync()
        => GetService<IAppCloseCoordinator>().RequestExplicitExitAsync(ExitApplicationAsync);

    private Task ExitApplicationAsync()
        => GetService<IAppShutdownCoordinator>().ShutdownAsync(StopHostAsync, Exit);

    private async Task StopHostAsync()
    {
        if (_hostStartTask is not null)
            await _hostStartTask.ConfigureAwait(false);

        await _host.StopAsync().ConfigureAwait(false);
    }
}
