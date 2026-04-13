using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed record ShellStateReadModel(AppCloseBehavior CloseBehavior, bool IsEngineReady);

public sealed class GetShellStateQuery(
    ITorrentEngineStatusService engineStatusService)
{
    public async Task<ShellStateReadModel> ExecuteAsync(CancellationToken ct = default)
    {
        return new ShellStateReadModel(AppCloseBehavior.MinimizeToTray, engineStatusService.IsReady);
    }
}
