using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Queries;

public sealed class GetTorrentListQuery(
    ITorrentCollectionRepository collectionRepository,
    ITorrentRuntimeFactsProvider runtimeFactsProvider)
{
    public async Task<IReadOnlyList<TorrentListItemReadModel>> ExecuteAsync(CancellationToken ct = default)
    {
        var entries = await collectionRepository.GetAllAsync(ct).ConfigureAwait(false);
        var facts = runtimeFactsProvider.GetAll();
        var matches = TorrentRuntimeFactMatcher.MatchEntries(entries, facts);

        return entries
            .Select(entry => TorrentListProjection.Project(entry, FindFact(entry, matches)))
            .OrderByDescending(x => x.AddedAtUtc)
            .ToList();
    }

    internal static TorrentRuntimeFact? FindFact(
        TorrentEntry entry,
        IReadOnlyDictionary<TorrentId, TorrentRuntimeFact> matches)
        => matches.TryGetValue(entry.Id, out var fact) ? fact : null;
}
