using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using MonoTorrent;
using NiTorrent.App.Services;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Infrastructure.DI;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Settings;
using NiTorrent.Presentation.Features.Shell;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using WinUIEx;
using WinUIApplication = Microsoft.UI.Xaml.Application;

namespace NiTorrent.App;

public partial class App : WinUIApplication
{
    private IHost _host;
    private bool _isExiting;
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
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IUriLauncher, WinUriLauncher>();
        services.AddSingleton<IPickerHelper, WinPickerHelper>();
        services.AddSingleton<IDialogService, WinUiDialogService>();
        services.AddSingleton<IUpdateService, DevWinUiUpdateService>();
        services.AddSingleton<IJsonNavigationService, JsonNavigationService>();
        services.AddSingleton<ITorrentPreviewDialogService, TorrentPreviewDialogService>();
    }

    protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {


        var mainInstance = AppInstance.FindOrRegisterForKey("main");

        // Если это не главный экземпляр — отправляем активацию главному и выходим
        if (!mainInstance.IsCurrent)
        {
            await mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit();
            return;
        }

        // Главный экземпляр: слушаем будущие активации (файлы/протоколы)
        mainInstance.Activated += (_, e) => _ = HandleActivationAsync(e);

        // Обработать “текущую” активацию (если запуск был через .torrent)
        _ = HandleActivationAsync(mainInstance.GetActivatedEventArgs());

        MainWindow = new MainWindow();

        MainWindow.Title = MainWindow.AppWindow.Title = ProcessInfoHelper.ProductNameAndVersion;
        MainWindow.AppWindow.SetIcon("Assets/AppIcon.ico");

        GetService<IThemeService>().Initialize(MainWindow);

        MainWindow.Activate();

        var tray = GetService<ITrayService>();
        tray.Initialize();
        tray.OpenRequested += ShowMainWindow;
        tray.ExitRequested += ExitAsync;
        // "Закрыть" = спрятать в трей
        MainWindow.AppWindow.Closing += (_, e) =>
        {
            if (_isExiting)
                return;

            e.Cancel = true;
            MainWindow.Hide();
            GetService<ITrayService>().SetVisible(true);
        };

        var holder = GetService<UiDispatcherHolder>();
        holder.Initialize(DispatcherQueue.GetForCurrentThread());

        InitializeApp();
    }
    private async Task HandleActivationAsync(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.File)
            return;

        if (args.Data is not FileActivatedEventArgs fileArgs)
            return;

        // Поднять главное окно (если было спрятано в трей)
        ShowMainWindow();

        var torrentPreviewDialog = GetService<ITorrentPreviewDialogService>();
        var torrentService = GetService<ITorrentService>();
        foreach (var item in fileArgs.Files)
        {
            if (item is StorageFile file &&
                file.FileType.Equals(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                var preview = await torrentService.GetPreviewAsync(new TorrentSource.TorrentFile(file.Path));
                var torrentPreviewDialogResult = await torrentPreviewDialog.ShowAsync(preview);
                if (torrentPreviewDialogResult is null)
                    return;
                await torrentService.AddAsync(new(new TorrentSource.TorrentFile(file.Path), torrentPreviewDialogResult.OutputFolder, torrentPreviewDialogResult.SelectedFilePaths.ToHashSet()));
            }
        }
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

    private Task InitializeTorrentEngineAsync()
    {
        try
        {
           return  GetService<ITorrentService>().InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            // позже можно показать диалог через IDialogService
        }
        return Task.CompletedTask;
    }

    private void ShowMainWindow()
    {
        GetService<IUiDispatcher>().EnqueueAsync(() =>
        {
            var win = MainWindow;
            win.Show();
            win.Activate();
        });

    }

    private async Task ExitAsync()
    {
        if (_isExiting)
            return;

        _isExiting = true;

        try
        {
            // Сохраняем
            //await GetService<ITorrentService>().SaveAsync();

            // Гасим трей
            GetService<TrayService>().Dispose();

            // Останавливаем host
            await _host.StopAsync();

            // Теперь реально закрываем окно (Closing больше не cancel-ится)
            MainWindow?.Close();
        }
        catch (Exception ex)
        {
            _host.Services.GetService<ILogger<App>>()?.LogError(ex, "Exit failed");
        }
        finally
        {
            Exit();
        }
    }
}

