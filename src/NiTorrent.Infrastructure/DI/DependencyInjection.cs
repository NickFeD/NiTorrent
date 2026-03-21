using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Infrastructure.Settings;
using NiTorrent.Infrastructure.Torrents;
using NiTorrent.Infrastructure.Torrents.LegacyAdapters;

namespace NiTorrent.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNiTorrentInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(AppConfigLoader.Load());
        services.AddSingleton<IAppPreferences, JsonAppPreferences>();
        services.AddSingleton<IAppShellSettingsRepository, JsonAppShellSettingsRepository>();
        services.AddSingleton<ITorrentEntrySettingsRepository, JsonTorrentEntrySettingsRepository>();
        services.AddSingleton<ITorrentEntrySettingsRuntimeApplier, TorrentEntrySettingsRuntimeApplier>();
        services.AddHostedService<TorrentMonitor>();
        services.AddSingleton<TorrentSnapshotFactory>();
        services.AddSingleton<TorrentRuntimeRegistry>();
        services.AddSingleton<TorrentEngineFactory>();
        services.AddSingleton<TorrentEngineStateStore>();
        services.AddSingleton<TorrentStartupRecovery>();
        services.AddSingleton<TorrentCommandExecutor>();
        services.AddSingleton<TorrentAddExecutor>();
        services.AddSingleton<TorrentSourceResolver>();
        services.AddSingleton<TorrentSettingsApplier>();
        services.AddSingleton<TorrentUpdatePublisher>();
        services.AddSingleton<TorrentCatalogSnapshotSynchronizer>();
        services.AddSingleton<TorrentEventOrchestrator>();
        services.AddSingleton<TorrentQueryService>();
        services.AddSingleton<BackgroundTaskRunner>();
        services.AddSingleton<TorrentLifecycleExecutor>();
        services.AddSingleton<TorrentNotifier>();
        services.AddSingleton<TorrentStartupCoordinator>();
        services.AddSingleton<TorrentRuntimeContext>();
        services.AddSingleton<ITorrentService, MonoTorrentService>();
        services.AddSingleton<ITorrentReadModelFeed, EngineBackedTorrentReadModelFeed>();
        services.AddSingleton<ITorrentWriteService, EngineBackedTorrentWriteService>();
        services.AddSingleton<ITorrentEngineStatusService, EngineBackedTorrentEngineStatusService>();
        services.AddSingleton<ITorrentEngineMaintenanceService, EngineBackedTorrentEngineMaintenanceService>();
        services.AddSingleton(TorrentConfigLoader.Load());
        services.AddSingleton<ITorrentPreferences, JsonTorrentPreferences>();
        services.AddSingleton<TorrentCatalogStore>();

        return services;
    }
}
