using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Settings;

public sealed class TorrentSettingsService(
    ITorrentSettingsRepository repository,
    ITorrentWriteService writeService) : ITorrentSettingsService
{
    public Task<TorrentSettingsDraft> LoadAsync(CancellationToken ct = default)
        => repository.LoadAsync(ct);

    public async Task SaveAndApplyAsync(TorrentSettingsDraft settings, CancellationToken ct = default)
    {
        var previous = await repository.LoadAsync(ct).ConfigureAwait(false);

        await repository.SaveAsync(settings, ct).ConfigureAwait(false);

        try
        {
            await writeService.ApplySettingsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await repository.SaveAsync(previous, ct).ConfigureAwait(false);
            throw new UserVisibleException("Не удалось применить настройки.", ex);
        }
    }
}
