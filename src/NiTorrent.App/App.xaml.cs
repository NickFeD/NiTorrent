using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.App.Services;
using NiTorrent.App.Services.AppLifecycle;
using NiTorrent.Application;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Infrastructure.DI;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Settings;
using WinUIApplication = Microsoft.UI.Xaml.Application;

namespace NiTorrent.App;

public partial class App : WinUIApplication
{
    private readonly IHost _host;
    private Task? _hostStartTask;

    public new static App Current => (App)WinUIApplication.Current;
    public static IHost Host => Current._host;

    public static Window MainWindow = new Window();
    public static IntPtr Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
    public IServiceProvider Services { get; }
    public IJsonNavigationService NavService => GetService<IJsonNavigationService>();

    public static T GetService<T>() where T : class
    {
        if ((Current as App)!.Services.GetService(typeof(T)) is not T service)
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");

        return service;
    }

    public App()
    {
        _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices(ConfigureServices)
            .Build();

        Services = _host.Services;
        InitializeComponent();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IThemeSettingsService, ThemeSettingsService>();
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
        services.AddSingleton<ITorrentPreviewService, TorrentPreviewDialogService>();
        services.AddSingleton<IAppStartupService, AppStartupService>();
        services.AddSingleton<IAppActivationService, AppActivationService>();
        services.AddSingleton<ThemeSettingsViewModel>();
        services.AddSingleton<NiTorrent.Application.Torrents.Queries.GetTorrentListQuery>();
        services.AddSingleton<GetTorrentListQuery>();
        services.AddSingleton<GetSettingsQuery>();
        services.AddSingleton<AppCloseCoordinator>();
        services.AddSingleton<IAppShutdownTask, HostStopShutdownTask>();
        services.AddTransient<RestoreSessionUseCase>();
        services.AddTransient<CreateTorrentDownloadUseCase>();
        services.AddTransient<PreviewTorrentContentsUseCase>();
        services.AddTransient<StartTorrentUseCase>();
        services.AddTransient<PauseTorrentUseCase>();
        services.AddTransient<DeleteTorrentUseCase>();
        services.AddTransient<UpdateSettingsUseCase>();
        services.AddTransient<AppStartupCoordinator>();
        services.AddSingleton<AppSettingsService>();
        services.AddTransient<IAppStartupTask>(t => t.GetRequiredService<AppSettingsService>());
        services.AddSingleton<MainWindowLifecycle>();
        services.AddSingleton<IAppShutdownTask>(sp => sp.GetRequiredService<MainWindowLifecycle>());
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainInstance = AppInstance.FindOrRegisterForKey("NiTorrent");
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

        var appStartup = GetService<AppStartupCoordinator>();

        //HACK : Start critical initialization before showing the main window to reduce time to interactive. This includes restoring the session which needs to be done before the main window is shown to avoid a visible delay after the main window is shown.
        await appStartup.StartCriticalAsync(CancellationToken.None);

        var window = GetService<MainWindowLifecycle>();
        MainWindow = window.CreateAndInitialize();
        window.Activate();

        _ = appStartup.StartBackgroundAsync(CancellationToken.None);
        _hostStartTask = _host.StartAsync();
    }

    private void StartBackgroundInitialization()
    {
        var startup = GetService<IAppStartupService>();
        _hostStartTask ??= startup.StartHostAndShellAsync(_host);
    }

    private void ShowMainWindow()
        => _ = GetService<MainWindowLifecycle>().ShowAsync();
}
