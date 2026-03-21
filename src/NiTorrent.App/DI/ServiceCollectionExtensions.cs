using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NiTorrent.App.Services;
using NiTorrent.App.Services.AppLifecycle;
using NiTorrent.App.Services.Windowing;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using WinUIEx;

namespace NiTorrent.App.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNiTorrentAppServices(this IServiceCollection services, string logsPath)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(logsPath));
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ContextMenuService>();
        services.AddSingleton<IAppStorageService, AppStorageService>();
        services.AddSingleton<UiDispatcherHolder>();
        services.AddSingleton<IUiDispatcher>(sp =>
        {
            var holder = sp.GetRequiredService<UiDispatcherHolder>();
            return new WinUiDispatcher(holder.Queue ?? throw new InvalidOperationException("UI Dispatcher not initialized"));
        });

        services.AddSingleton<IAppInfo, DevWinAppInfo>();
        services.AddSingleton<IMainWindowAccessor, MainWindowAccessor>();
        services.AddSingleton<ITorrentSpeedSummaryService, TorrentSpeedSummaryService>();
        services.AddSingleton<IAppShellSettingsService, JsonAppShellSettingsService>();
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
        services.AddSingleton<IAppCloseCoordinator, AppCloseCoordinator>();
        services.AddSingleton<IAppShutdownCoordinator, AppShutdownCoordinator>();

        return services;
    }

    public static IServiceCollection AddNiTorrentApplicationWorkflows(this IServiceCollection services)
    {
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
        return services;
    }
}
