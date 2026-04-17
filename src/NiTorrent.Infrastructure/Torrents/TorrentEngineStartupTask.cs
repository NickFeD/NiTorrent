using NiTorrent.Application;
using NiTorrent.Application.Settings;

namespace NiTorrent.Infrastructure.Torrents;

internal class TorrentEngineStartupTask(IEngineSettingsService settingsService) : IAppStartupTask
{
    private readonly IEngineSettingsService _settingsService = settingsService;

    public StartupStage Stage => StartupStage.Background;

    public int Order => 200;

    public bool CanRunInParallel => true;

    public Task ExecuteAsync(CancellationToken ct)
    {
        return _settingsService.InitializeAsync(ct);
    }
}
