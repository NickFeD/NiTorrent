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
using NiTorrent.Torrents.Contracts;

namespace NiTorrent.Infrastructure.DI;

/// <summary>
/// Registers product state and host tasks. The composition root supplies an engine adapter.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNiTorrentInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ITorrentRepository, JsonTorrentRepository>();
        services.AddSingleton<InMemoryTorrentRuntimeStateSource>();
        services.AddSingleton<ITorrentRuntimeStateSource>(sp => sp.GetRequiredService<InMemoryTorrentRuntimeStateSource>());
        services.AddSingleton<ITorrentObservations>(sp => sp.GetRequiredService<InMemoryTorrentRuntimeStateSource>());
        services.AddSingleton<ITorrentDownloadFactory, TorrentDownloadFactory>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<SetAllTorrentStatesUseCase>();
        services.AddSingleton<TorrentsModule>();
        services.AddSingleton<ITorrentsModule>(sp => sp.GetRequiredService<TorrentsModule>());
        services.AddSingleton<ITorrentsLifetime>(sp => sp.GetRequiredService<TorrentsModule>());
        services.AddSingleton<GetSettingsQuery>();
        services.AddSingleton<UpdateSettingsUseCase>();
        services.AddSingleton<ITorrentSettings, TorrentSettingsModule>();
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
