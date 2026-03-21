using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Presentation.Features.Settings;
using NiTorrent.Presentation.Features.Shell;
using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddNiTorrentPresentation(this IServiceCollection services)
    {
        // Shell
        services.AddTransient<MainViewModel>();

        // Settings
        services.AddTransient<AboutUsSettingViewModel>();
        services.AddTransient<TorrentSettingsViewModel>();
        services.AddTransient<AppUpdateSettingViewModel>();

        // Torrents
        services.AddTransient<TorrentViewModel>();
        services.AddTransient<TorrentPreviewViewModel>();
        return services;
    }
}
