using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using NiTorrent.App.Services;
using NiTorrent.Application.Abstractions;
using NiTorrent.Infrastructure.DI;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Settings;
using NiTorrent.Presentation.Features.Shell;
using WinUIApplication = Microsoft.UI.Xaml.Application;

namespace NiTorrent.App;

public partial class App : WinUIApplication
{
    private IHost _host;
    public new static App Current => (App)WinUIApplication.Current;
    public static Window MainWindow = Window.Current;
    public static IntPtr Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
    public IServiceProvider Services { get; }
    public IJsonNavigationService NavService => GetService<IJsonNavigationService>();

    public static T GetService<T>() where T : class
    {
        if ((App.Current as App)!.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public App()
    {
        _host = Microsoft.Extensions.Hosting.Host
           .CreateDefaultBuilder()
           .UseContentRoot(AppContext.BaseDirectory)
           .ConfigureServices(ConfigureServices)
           .Build();
        Services = _host.Services;

        this.InitializeComponent();

    }

    private static void ConfigureServices(HostBuilderContext contexts, IServiceCollection services)
    {
        // DevWinUI
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ContextMenuService>();

        services.AddSingleton<IAppStorageService, AppStorageService>();

        // Наши слои
        services.AddNiTorrentInfrastructure();
        services.AddNiTorrentPresentation();

        // App implementations
        services.AddLogging();
        services.AddSingleton<UiDispatcherHolder>();
        services.AddSingleton<IUiDispatcher>(sp =>
        {
            var holder = sp.GetRequiredService<UiDispatcherHolder>();
            return new WinUiDispatcher(holder.Queue ?? throw new InvalidOperationException("UI Dispatcher not initialized"));
        });
        services.AddSingleton<IAppInfo, DevWinAppInfo>();
        services.AddSingleton<IPickerHelper, WinPickerHelper>();
        services.AddSingleton<IUriLauncher, WinUriLauncher>();
        services.AddSingleton<IDialogService, WinUiDialogService>();
        services.AddSingleton<IUpdateService, DevWinUiUpdateService>();
        services.AddSingleton<IJsonNavigationService, JsonNavigationService>();
        services.AddSingleton<ITorrentPreviewDialogService, TorrentPreviewDialogService>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {

        MainWindow = new MainWindow();

        MainWindow.Title = MainWindow.AppWindow.Title = ProcessInfoHelper.ProductNameAndVersion;
        MainWindow.AppWindow.SetIcon("Assets/AppIcon.ico");

        GetService<IThemeService>().Initialize(MainWindow);

        MainWindow.Activate();

        MainWindow.Closed += async (_, __) =>
        {
            await _host.StopAsync();
            _host.Dispose();
        };
        var holder = GetService<UiDispatcherHolder>();
        holder.Initialize(DispatcherQueue.GetForCurrentThread());

        InitializeApp();
    }

    private async void InitializeApp()
    {
        await _host.StartAsync();
        var menuService = GetService<ContextMenuService>();
        if (menuService != null && RuntimeHelper.IsPackaged())
        {
            ContextMenuItem menu = new ContextMenuItem
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

            await menuService.SaveAsync(menu);
        }
        _ = InitializeTorrentEngineAsync();
    }


    private async Task InitializeTorrentEngineAsync()
    {
        try
        {
            await GetService<ITorrentService>().InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            // позже можно показать диалог через IDialogService
        }
    }
}

