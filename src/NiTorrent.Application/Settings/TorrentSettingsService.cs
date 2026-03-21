using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Settings;

public sealed class TorrentSettingsService(
    ITorrentSettingsRepository repository,
    ITorrentService torrentService) : ITorrentSettingsService
{
    public Task<TorrentSettingsDraft> LoadAsync(CancellationToken ct = default)
        => repository.LoadAsync(ct);

    public async Task SaveAndApplyAsync(TorrentSettingsDraft settings, CancellationToken ct = default)
    {
        await repository.SaveAsync(settings, ct).ConfigureAwait(false);
        await torrentService.ApplySettingsAsync().ConfigureAwait(false);
    }
}
