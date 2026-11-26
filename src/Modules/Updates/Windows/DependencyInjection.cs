using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.Updates.Windows;

public static class UpdatesRegistration
{
    public static IServiceCollection AddNiTorrentUpdates(this IServiceCollection services) =>
        services.AddSingleton<IUpdateService, DevWinUiUpdateService>();
}
