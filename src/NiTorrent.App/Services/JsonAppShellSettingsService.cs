using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;
using NiTorrent.Infrastructure.Settings;

namespace NiTorrent.App.Services;

public sealed class JsonAppShellSettingsService(TorrentConfig config) : IAppShellSettingsService
{
    public AppCloseBehavior GetCloseBehavior()
        => config.MinimizeToTrayOnClose ? AppCloseBehavior.MinimizeToTray : AppCloseBehavior.ExitApplication;

    public Task SaveCloseBehaviorAsync(AppCloseBehavior behavior, CancellationToken ct = default)
    {
        config.MinimizeToTrayOnClose = behavior == AppCloseBehavior.MinimizeToTray;
        config.Save();
        return Task.CompletedTask;
    }
}
