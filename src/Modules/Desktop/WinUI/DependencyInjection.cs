using NiTorrent.App.Services;
using NiTorrent.App.Services.AppLifecycle;
using NiTorrent.Application.Abstractions;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Settings;
using Windows.Storage;

namespace NiTorrent.App;

public static class DesktopRegistration
{
    public static IServiceCollection AddNiTorrentDesktop(this IServiceCollection services)
    {
        services.AddNiTorrentPresentation();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IThemeSettingsService, ThemeSettingsService>();
        services.AddSingleton(_ => ApplicationData.Current.LocalFolder);
        services.AddSingleton<ContextMenuService>();
        services.AddSingleton<UiDispatcherHolder>();
        services.AddSingleton<IUiDispatcher>(sp => new WinUiDispatcher(
            sp.GetRequiredService<UiDispatcherHolder>().Queue ?? throw new InvalidOperationException("UI Dispatcher not initialized")));
        services.AddSingleton<IAppInfo, DevWinAppInfo>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IUriLauncher, WinUriLauncher>();
        services.AddSingleton<IFolderLauncher, FolderLauncher>();
        services.AddSingleton<IPickerHelper, WinPickerHelper>();
        services.AddSingleton<IDialogService, WinUiDialogService>();
        services.AddSingleton<IStartupErrorPresenter, StartupErrorPresenter>();
        services.AddSingleton<IJsonNavigationService, JsonNavigationService>();
        services.AddSingleton<ITorrentPreviewService, TorrentPreviewDialogService>();
        services.AddSingleton<IAppActivationService, AppActivationService>();
        services.AddSingleton<IActivationQueue, ActivationQueue>();
        services.AddSingleton<AppShutdownService>();
        services.AddSingleton<IAppShutdownService>(sp => sp.GetRequiredService<AppShutdownService>());
        services.AddHostedService<ContextMenuStartupTask>();
        services.AddSingleton<MainWindowLifecycle>();
        services.AddSingleton<IAppShellLifecycle>(sp => sp.GetRequiredService<MainWindowLifecycle>());
        services.AddSingleton<ThemeSettingsViewModel>();
        return services;
    }
}
