using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.Deferred;
using NiTorrent.Application.Torrents.Restore;
using NiTorrent.Infrastructure.Settings;
using NiTorrent.Infrastructure.Torrents;

namespace NiTorrent.Infrastructure.DI;

/// <summary>
/// Authoritative infrastructure composition root.
/// Keep all runtime registrations here and treat the legacy shim in
/// <c>NiTorrent.Infrastructure.DependencyInjection</c> as compatibility-only.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNiTorrentInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TorrentEntrySettingsConfigLoader.Load());
        services.AddSingleton<ILegacyTorrentEntrySettingsMigrationSource, LegacyTorrentEntrySettingsMigrationSource>();
        services.AddSingleton<ITorrentEntrySettingsRuntimeApplier, TorrentEntrySettingsRuntimeApplier>();
        services.AddHostedService<TorrentMonitor>();
        services.AddSingleton<TorrentRuntimeRegistry>();
        services.AddSingleton<TorrentStableKeyAccessor>();
        services.AddSingleton<TorrentEngineFactory>();
        services.AddSingleton<TorrentEngineStateStore>();
        services.AddSingleton<TorrentStartupRecovery>();
        services.AddSingleton<TorrentAddExecutor>();
        services.AddSingleton<TorrentSourceResolver>();
        services.AddSingleton<ITorrentSourcePreparationService>(sp => sp.GetRequiredService<TorrentSourceResolver>());
        services.AddSingleton<TorrentSettingsApplier>();
        services.AddSingleton<PeerEndpointConnectionCooldown>();
        services.AddSingleton<TorrentEventOrchestrator>();
        services.AddSingleton<BackgroundTaskRunner>();
        services.AddSingleton<TorrentLifecycleExecutor>();
        services.AddSingleton<TorrentStartupCoordinator>();
        services.AddSingleton<TorrentRuntimeContext>();
        services.AddSingleton<ITorrentSourceStore, TorrentSourceStore>();
        services.AddSingleton<ITorrentCollectionRepository, CatalogBackedTorrentCollectionRepository>();
        services.AddSingleton<ITorrentReadModelFeed, EngineBackedTorrentReadModelFeed>();
        services.AddSingleton<ITorrentWriteService, EngineBackedTorrentWriteService>();
        services.AddSingleton<ITorrentDetailsRuntimeService, EngineBackedTorrentDetailsRuntimeService>();
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
        services.AddSingleton<ITorrentSettingsRepository, TorrentConfigSettingsRepository>();
        services.AddSingleton<TorrentCatalogStore>();

        return services;
    }
}
