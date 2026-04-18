using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Infrastructure.Settings;
using NiTorrent.Infrastructure.Torrents;
using Nucs.JsonSettings;

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
        ;
        services.AddSingleton<ITorrentRuntimeStatusProvider, TorrentRuntimeStatusProvider>();
        services.AddSingleton<ITorrentRepository, JsonTorrentRepository>();
        services.AddSingleton<ITorrentRuntimeStateSource, InMemoryTorrentRuntimeStateSource>();
        services.AddSingleton<ITorrentRuntimeGateway, TorrentRuntimeGateway>();
        services.AddSingleton<ITorrentDownloadFactory, TorrentDownloadFactory>();
        services.AddSingleton<ITorrentMetadataProvider, TorrentMetadataProvider>();
        services.AddTransient<IAppStartupTask, TorrentEngineStartupTask>();
        services.AddSingleton<IEngineSettingsService, EngineSettingsService>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddTransient<IAppShutdownTask, SettingsRepositoryFlushShutdownTask>();
        services.AddSingleton<TorrentEngineCoordinator>();
        services.AddSingleton<AppJsonSettings>(sp =>
        {
            var storage = sp.GetRequiredService<IAppStorageService>();
            var path = storage.GetLocalPath("torrent_settings.json");
            storage.EnsureParentDirectory(path);

            return JsonSettings.Load<AppJsonSettings>(path);
        });

        services.AddTransient<IAppStartupTask, TorrentEngineStartupTask>();
        return services;
    }
}
