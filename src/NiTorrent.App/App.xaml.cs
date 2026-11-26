using NiTorrent.Updates.Windows;
using NiTorrent.Torrents.Engines.MonoTorrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.App.Services;
using NiTorrent.App.Services.AppLifecycle;
using NiTorrent.Application;
using NiTorrent.Application.Abstractions;



using NiTorrent.Infrastructure.DI;

using NiTorrent.Presentation.Abstractions;


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
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices(ConfigureServices)
            .Build();
        EarlyLifecycleLogger.Log($"Host built in {elapsed.ElapsedMilliseconds} ms");

        Services = _host.Services;
        DesktopServices.Initialize(Services);
        _application = new NiTorrentApplication(_host, Services, Exit);
        GetService<AppShutdownService>().Initialize(() => _application.ShutdownAsync(AppShutdownReason.UserRequested, CancellationToken.None));
        elapsed.Restart();
        InitializeComponent();
        EarlyLifecycleLogger.Log($"App resources loaded in {elapsed.ElapsedMilliseconds} ms");
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var storage = new AppStorageService();

        services.AddSingleton<IAppStorageService>(storage);

        var logsPath = storage.GetLocalPath(Path.Combine("Logs", $"app-{DateTime.Now:yyyyMMdd}.log"));

        services.AddNiTorrentInfrastructure();
        services.AddMonoTorrentEngine();
        services.AddNiTorrentDesktop();
        services.AddNiTorrentUpdates();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(logsPath));
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IShutdownStateService, ShutdownStateService>();

    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var currentInstance = AppInstance.GetCurrent();
        currentInstance.Activated += (_, e) => _ = _application.HandleActivationAsync(e, CancellationToken.None);

        await _application.StartAsync(currentInstance.GetActivatedEventArgs(), CancellationToken.None);
    }
}
