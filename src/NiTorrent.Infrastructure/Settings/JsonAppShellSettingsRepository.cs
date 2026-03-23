using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Infrastructure.Settings;

public sealed class JsonAppShellSettingsRepository : IAppShellSettingsRepository
{
    private readonly TorrentConfig _config;

    public JsonAppShellSettingsRepository(TorrentConfig config)
    {
        _config = config;
    }

    public AppShellSettings Load()
        => new()
        {
            CloseBehavior = ReadCloseBehavior(),
        };

    public void Save(AppShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _config.CloseBehavior = settings.CloseBehavior.ToString();
        _config.MinimizeToTrayOnClose = settings.CloseBehavior == AppCloseBehavior.MinimizeToTray;
        _config.Save();
    }

    private AppCloseBehavior ReadCloseBehavior()
    {
        if (!string.IsNullOrWhiteSpace(_config.CloseBehavior)
            && Enum.TryParse<AppCloseBehavior>(_config.CloseBehavior, true, out var parsed))
        {
            return parsed;
        }

        return _config.MinimizeToTrayOnClose
            ? AppCloseBehavior.MinimizeToTray
            : AppCloseBehavior.ExitApplication;
    }
}
