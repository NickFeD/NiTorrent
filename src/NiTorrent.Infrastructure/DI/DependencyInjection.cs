using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Abstractions;
using NiTorrent.Infrastructure.Settings;
using NiTorrent.Infrastructure.Torrents;

namespace NiTorrent.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNiTorrentInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(AppConfigLoader.Load());
        services.AddSingleton<IAppPreferences, JsonAppPreferences>();
        services.AddHostedService<TorrentMonitor>();
        services.AddSingleton<ITorrentService, MonoTorrentService>();

        return services;
    }
}
