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
    private Task? _startupTask;
    private int _shutdownStarted;

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
        RegisterHostShutdown();
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
        services.AddSingleton<IAppActivationService, AppActivationService>();
        services.AddSingleton<IAppShutdownService, AppShutdownService>();
        services.AddSingleton<AppLifecycleCoordinator>();
        services.AddHostedService<AppHostLifecycleService>();
        services.AddTransient<IAppStartupTask, ContextMenuStartupTask>();
        services.AddSingleton<MainWindowLifecycle>();
        services.AddSingleton<IAppShellLifecycle>(sp => sp.GetRequiredService<MainWindowLifecycle>());
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
        services.AddTransient<IAppStartupTask>(t => t.GetRequiredService<AppSettingsService>());
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

        mainInstance.Activated += (_, e) => _ = HandleActivationAsync(e);

        _startupTask = StartApplicationAsync();
        await _startupTask;
        await HandleActivationAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
    }

    private async Task StartApplicationAsync()
    {
        var holder = GetService<UiDispatcherHolder>();
        holder.Initialize(DispatcherQueue.GetForCurrentThread());

        await _host.StartAsync();
        await GetService<IAppShellLifecycle>().StartAsync();
    }

    private async Task HandleActivationAsync(AppActivationArguments args)
    {
        if (_startupTask is not null)
            await _startupTask;

        await GetService<IAppActivationService>().HandleAsync(args);
    }

    private void RegisterHostShutdown()
    {
        var lifetime = Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() => _ = StopHostAndExitAsync());
    }

    private async Task StopHostAndExitAsync()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
            return;

        var dispatcher = GetService<IUiDispatcher>();
        var logger = Services.GetService<ILogger<App>>();
        var shellLifecycle = GetService<IAppShellLifecycle>();

        try
        {
            await _host.StopAsync();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Host shutdown failed");
        }

        try
        {
            await shellLifecycle.CloseAsync();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Shell shutdown failed");
        }
        finally
        {
            _host.Dispose();
            await dispatcher.EnqueueAsync(Exit);
        }
    }
}
