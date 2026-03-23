using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.Deferred;
using NiTorrent.Application.Torrents.Restore;
using NiTorrent.Infrastructure.Settings;
using NiTorrent.Infrastructure.Torrents;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;

namespace NiTorrent.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNiTorrentInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(AppConfigLoader.Load());
        services.AddSingleton<IAppPreferences, JsonAppPreferences>();
        services.AddSingleton<IAppShellSettingsRepository, JsonAppShellSettingsRepository>();
        services.AddSingleton<ITorrentSettingsRepository, TorrentConfigSettingsRepository>();
        services.AddSingleton<ITorrentSettingsService, TorrentSettingsService>();
        services.AddSingleton(TorrentEntrySettingsConfigLoader.Load());
        services.AddSingleton<ITorrentEntrySettingsRepository, TorrentEntrySettingsConfigRepository>();
        services.AddSingleton<ITorrentEntrySettingsRuntimeApplier, TorrentEntrySettingsRuntimeApplier>();
        services.AddHostedService<TorrentMonitor>();
        services.AddSingleton<TorrentRuntimeRegistry>();
        services.AddSingleton<TorrentEngineFactory>();
        services.AddSingleton<TorrentEngineStateStore>();
        services.AddSingleton<TorrentStartupRecovery>();
        services.AddSingleton<TorrentAddExecutor>();
        services.AddSingleton<TorrentSourceResolver>();
        services.AddSingleton<TorrentSettingsApplier>();
        services.AddSingleton<TorrentEventOrchestrator>();
        services.AddSingleton<BackgroundTaskRunner>();
        services.AddSingleton<TorrentLifecycleExecutor>();
        services.AddSingleton<TorrentNotifier>();
        services.AddSingleton<TorrentStartupCoordinator>();
        services.AddSingleton<TorrentRuntimeContext>();
        services.AddSingleton<ITorrentCollectionRepository, CatalogBackedTorrentCollectionRepository>();
        services.AddSingleton<ITorrentReadModelFeed, EngineBackedTorrentReadModelFeed>();
        services.AddSingleton<ITorrentWriteService, EngineBackedTorrentWriteService>();
        services.AddSingleton<ITorrentRuntimeFactsProvider, RuntimeBackedTorrentRuntimeFactsProvider>();
        services.AddSingleton<ITorrentEngineGateway, InfrastructureTorrentEngineGateway>();
        services.AddSingleton<ITorrentEngineLifecycle, InfrastructureTorrentEngineLifecycle>();
        services.AddSingleton<ITorrentEngineStateStore, InfrastructureTorrentEngineStateStore>();
        services.AddSingleton<ITorrentCommandService, TorrentCommandService>();
        services.AddSingleton<IApplyDeferredTorrentActionsWorkflow, ApplyDeferredTorrentActionsWorkflow>();
        services.AddSingleton<IRestoreTorrentCollectionWorkflow, RestoreTorrentCollectionWorkflow>();
        services.AddSingleton<ITorrentEngineStatusService, EngineBackedTorrentEngineStatusService>();
        services.AddSingleton<ITorrentEngineMaintenanceService, EngineBackedTorrentEngineMaintenanceService>();
        services.AddSingleton(TorrentConfigLoader.Load());
        services.AddSingleton<ITorrentPreferences, JsonTorrentPreferences>();
        services.AddSingleton<TorrentCatalogStore>();

        return services;
    }
}
