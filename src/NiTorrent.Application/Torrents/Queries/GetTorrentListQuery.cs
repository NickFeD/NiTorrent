using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents.Queries;

public sealed class GetTorrentListQuery(ITorrentCollectionRepository collectionRepository)
{
    public async Task<IReadOnlyList<TorrentListItemReadModel>> ExecuteAsync(CancellationToken ct = default)
    {
        var entries = await collectionRepository.GetAllAsync(ct).ConfigureAwait(false);

        return entries
            .Where(x => x.Intent != NiTorrent.Domain.Torrents.TorrentIntent.Removed)
            .Select(TorrentListProjection.Project)
            .OrderByDescending(x => x.AddedAtUtc)
            .ToList();
    }
}
