using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Infrastructure.Settings;
using NiTorrent.Infrastructure.Torrents;

namespace NiTorrent.Torrents.Engines.MonoTorrent;

public static class MonoTorrentRegistration
{
    public static IServiceCollection AddMonoTorrentEngine(this IServiceCollection services)
    {
        services.AddSingleton<ITorrentRuntimeStatusProvider, TorrentRuntimeStatusProvider>();
        services.AddSingleton<TorrentRuntimeWorkGate>();
        services.AddSingleton<ITorrentRuntimeGateway, TorrentRuntimeGateway>();
        services.AddSingleton<ITorrentEngineLifecycle, TorrentEngineLifecycle>();
        services.AddSingleton<ITorrentMetadataProvider, TorrentMetadataProvider>();
        services.AddSingleton<IEngineSettingsService, EngineSettingsService>();
        services.AddSingleton<TorrentEngineCoordinator>();
        return services;
    }
}
