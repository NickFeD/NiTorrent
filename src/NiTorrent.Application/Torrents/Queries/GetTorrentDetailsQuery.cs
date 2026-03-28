using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Queries;

public sealed class GetTorrentDetailsQuery(
    ITorrentCollectionRepository collectionRepository,
    ITorrentEntrySettingsRepository settingsRepository)
{
    public async Task<TorrentDetailsReadModel?> ExecuteAsync(TorrentId torrentId, CancellationToken ct = default)
    {
        var entry = await collectionRepository.TryGetAsync(torrentId, ct).ConfigureAwait(false);
        if (entry is null)
            return null;

        var status = TorrentListProjection.ResolveStatus(entry);
        var effectiveSettings = entry.PerTorrentSettings ?? settingsRepository.Load(torrentId);

        return new TorrentDetailsReadModel(
            entry.Id,
            entry.Key.Value,
            entry.Name,
            entry.Size,
            entry.SavePath,
            entry.AddedAtUtc,
            status,
            entry.HasMetadata,
            entry.SelectedFiles,
            effectiveSettings);
    }
}
