using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.UseCase;
using Microsoft.Extensions.Hosting;
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
        services.AddSingleton<ITorrentRuntimeStatusProvider, TorrentRuntimeStatusProvider>();
        services.AddSingleton<ITorrentRepository, JsonTorrentRepository>();
        services.AddSingleton<TorrentRuntimeWorkGate>();
        services.AddSingleton<InMemoryTorrentRuntimeStateSource>();
        services.AddSingleton<ITorrentRuntimeStateSource>(sp => sp.GetRequiredService<InMemoryTorrentRuntimeStateSource>());
        services.AddSingleton<ITorrentRuntimeGateway, TorrentRuntimeGateway>();
        services.AddSingleton<ITorrentEngineLifecycle, TorrentEngineLifecycle>();
        services.AddSingleton<ITorrentDownloadFactory, TorrentDownloadFactory>();
        services.AddSingleton<ITorrentMetadataProvider, TorrentMetadataProvider>();
        services.AddSingleton<IEngineSettingsService, EngineSettingsService>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<TorrentEngineCoordinator>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<SetAllTorrentStatesUseCase>();
        services.AddSingleton<TorrentSession>();
        services.AddSingleton<ITorrentSession>(sp => sp.GetRequiredService<TorrentSession>());
        services.AddSingleton<CoreShutdownState>();
        services.Configure<HostOptions>(options =>
        {
            options.ServicesStartConcurrently = false;
            options.ServicesStopConcurrently = false;
            options.ShutdownTimeout = TimeSpan.FromSeconds(10);
        });
        services.AddHostedService<StorageHostedService>();
        services.AddHostedService<TorrentSessionHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<InMemoryTorrentRuntimeStateSource>());
        services.AddSingleton(sp => new Lazy<AppJsonSettings>(() =>
        {
            var storage = sp.GetRequiredService<IAppStorageService>();
            var path = storage.GetLocalPath("torrent_settings.json");
            storage.EnsureParentDirectory(path);

            return JsonSettings.Load<AppJsonSettings>(path);
        }));
        return services;
    }
}
