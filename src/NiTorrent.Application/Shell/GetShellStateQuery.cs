using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed record ShellStateReadModel(AppCloseBehavior CloseBehavior, bool IsEngineReady);

public sealed class GetShellStateQuery(
    ITorrentSettingsRepository settingsRepository,
    ITorrentEngineStatusService engineStatusService)
{
    public async Task<ShellStateReadModel> ExecuteAsync(CancellationToken ct = default)
    {
        var settings = await settingsRepository.LoadAsync(ct).ConfigureAwait(false);
        return new ShellStateReadModel(settings.CloseBehavior, engineStatusService.IsReady);
    }
}
