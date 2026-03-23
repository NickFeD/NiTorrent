using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class UpdatePerTorrentSettingsWorkflow(
    ITorrentEntrySettingsRepository settingsRepository,
    ITorrentEntrySettingsRuntimeApplier runtimeApplier,
    NiTorrent.Application.Abstractions.ITorrentCollectionRepository collectionRepository)
{
    public async Task ExecuteAsync(TorrentId torrentId, TorrentEntrySettings settings, CancellationToken ct = default)
    {
        settingsRepository.Save(torrentId, settings);

        var entry = await collectionRepository.TryGetAsync(torrentId, ct).ConfigureAwait(false);
        if (entry is not null)
        {
            await collectionRepository.UpsertAsync(entry.WithPerTorrentSettings(settings), ct).ConfigureAwait(false);
            await collectionRepository.SaveAsync(ct).ConfigureAwait(false);
        }

        await runtimeApplier.ApplyAsync(torrentId, settings, ct).ConfigureAwait(false);
    }
}
