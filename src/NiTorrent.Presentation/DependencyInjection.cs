using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Presentation.Features.Settings;
using NiTorrent.Presentation.Features.Shell;

namespace NiTorrent.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddNiTorrentPresentation(this IServiceCollection services)
    {
        // Shell
        services.AddTransient<MainViewModel>();

        // Settings
        services.AddTransient<AppUpdateSettingViewModel>();
        services.AddTransient<AboutUsSettingViewModel>();

        // Torrents
        return services;
    }
}
