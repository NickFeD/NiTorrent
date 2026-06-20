using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
using Windows.Storage;
using WinUIApplication = Microsoft.UI.Xaml.Application;

namespace NiTorrent.App;

public partial class App : WinUIApplication
{
    private readonly IHost _host;
    private readonly INiTorrentApplication _application;

    public new static App Current => (App)WinUIApplication.Current;

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
        _application = new NiTorrentApplication(_host, Services, Exit);
        GetService<AppShutdownService>().Initialize(() => _application.ShutdownAsync(AppShutdownReason.UserRequested, CancellationToken.None));
        InitializeComponent();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var storage = new AppStorageService();

        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IThemeSettingsService, ThemeSettingsService>();
        services.AddSingleton(_ => ApplicationData.Current.LocalFolder);
        services.AddSingleton<ContextMenuService>();
        services.AddSingleton<IAppStorageService>(storage);

        var logsPath = storage.GetLocalPath(Path.Combine("Logs", $"app-{DateTime.Now:yyyyMMdd}.log"));

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
        services.AddSingleton<IAppActivationService, AppActivationService>();
        services.AddSingleton<IActivationQueue, ActivationQueue>();
        services.AddSingleton<ShutdownTimeoutOptions>();
        services.AddSingleton<IShutdownStateService, ShutdownStateService>();
        services.AddSingleton<AppShutdownService>();
        services.AddSingleton<IAppShutdownService>(sp => sp.GetRequiredService<AppShutdownService>());
        services.AddSingleton<AppLifecycleCoordinator>();
        services.AddHostedService<AppHostLifecycleService>();
        services.AddTransient<IAppLifecycleTask, ContextMenuStartupTask>();
        services.AddTransient<IAppLifecycleTask, ShutdownStateStartupTask>();
        services.AddSingleton<MainWindowLifecycle>();
        services.AddSingleton<IAppShellLifecycle>(sp => sp.GetRequiredService<MainWindowLifecycle>());
        services.AddTransient<IAppLifecycleTask, MainWindowLifecycleTask>();
        services.AddTransient<IAppLifecycleTask, RestoreSessionLifecycleTask>();
        services.AddTransient<IAppLifecycleTask, TrayLifecycleTask>();
        services.AddTransient<IAppLifecycleTask, ReadyForActivationLifecycleTask>();
        services.AddTransient<IAppLifecycleTask, TorrentRuntimeStopAcceptingWorkTask>();
        services.AddTransient<IAppLifecycleTask, TorrentRuntimeStopTask>();
        services.AddTransient<IAppLifecycleTask, TorrentRuntimeFlushStateTask>();
        services.AddSingleton<ThemeSettingsViewModel>();
        services.AddSingleton<NiTorrent.Application.Torrents.Queries.GetTorrentListQuery>();
        services.AddSingleton<GetTorrentListQuery>();
        services.AddSingleton<GetSettingsQuery>();
        services.AddTransient<RestoreSessionUseCase>();
        services.AddTransient<CreateTorrentDownloadUseCase>();
        services.AddTransient<PreviewTorrentContentsUseCase>();
        services.AddTransient<StartTorrentUseCase>();
        services.AddTransient<PauseTorrentUseCase>();
        services.AddTransient<DeleteTorrentUseCase>();
        services.AddTransient<UpdateSettingsUseCase>();
        services.AddSingleton<AppSettingsService>();
        services.AddTransient<IAppLifecycleTask>(t => t.GetRequiredService<AppSettingsService>());
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var currentInstance = AppInstance.GetCurrent();
        currentInstance.Activated += (_, e) => _ = _application.HandleActivationAsync(e, CancellationToken.None);

        await _application.StartAsync(currentInstance.GetActivatedEventArgs(), CancellationToken.None);
    }
}
