using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed record ShellStateReadModel(AppCloseBehavior CloseBehavior, bool IsEngineReady);

public sealed class GetShellStateQuery(
    IAppShellSettingsService shellSettings,
    NiTorrent.Application.Torrents.ITorrentEngineStatusService engineStatusService)
{
    public ShellStateReadModel Execute()
        => new(shellSettings.GetCloseBehavior(), engineStatusService.IsReady);
}
