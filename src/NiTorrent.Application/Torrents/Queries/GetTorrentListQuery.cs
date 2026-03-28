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

        var byId = facts
            .Where(x => x.Id is not null)
            .GroupBy(x => x.Id!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var byKey = facts
            .Where(x => !x.Key.IsEmpty)
            .GroupBy(x => x.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return entries
            .Select(entry => TorrentListProjection.Project(entry, FindFact(entry, byId, byKey)))
            .OrderByDescending(x => x.AddedAtUtc)
            .ToList();
    }

    internal static TorrentRuntimeFact? FindFact(
        TorrentEntry entry,
        IReadOnlyDictionary<TorrentId, TorrentRuntimeFact> byId,
        IReadOnlyDictionary<string, TorrentRuntimeFact> byKey)
    {
        if (byId.TryGetValue(entry.Id, out var byEntryId))
            return byEntryId;

        if (!entry.Key.IsEmpty && byKey.TryGetValue(entry.Key.Value, out var byEntryKey))
            return byEntryKey;

        return null;
    }
}
