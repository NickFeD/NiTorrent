using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed record ShellStateReadModel(AppCloseBehavior CloseBehavior, bool IsEngineReady);

public sealed class GetShellStateQuery(
    ITorrentSettingsRepository settingsRepository,
    NiTorrent.Application.Torrents.ITorrentEngineStatusService engineStatusService)
{
    public ShellStateReadModel Execute()
        => new(settingsRepository.LoadAsync().GetAwaiter().GetResult().CloseBehavior, engineStatusService.IsReady);
}
