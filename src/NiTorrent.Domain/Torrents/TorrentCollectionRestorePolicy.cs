namespace NiTorrent.Domain.Torrents;

public static class TorrentCollectionRestorePolicy
{
    public static IReadOnlyList<TorrentEntry> ApplyRuntimeFacts(
        IReadOnlyList<TorrentEntry> entries,
        IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
    {
        var factsById = runtimeFacts.Where(x => x.Id is not null)
            .ToDictionary(x => x.Id!.Value, x => x);

        var factsByKey = runtimeFacts.Where(x => !x.Key.IsEmpty)
            .GroupBy(x => x.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<TorrentEntry>(entries.Count);
        foreach (var entry in entries)
        {
            TorrentRuntimeFact? fact = null;
            if (factsById.TryGetValue(entry.Id, out var byId))
            {
                fact = byId;
            }
            else if (!entry.Key.IsEmpty && factsByKey.TryGetValue(entry.Key.Value, out var byKey))
            {
                fact = byKey;
            }

            if (fact is null)
            {
                result.Add(entry.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(entry)));
                continue;
            }

            var merged = entry with
            {
                Key = fact.Key.IsEmpty ? entry.Key : fact.Key,
                Name = string.IsNullOrWhiteSpace(fact.Name) ? entry.Name : fact.Name,
                Size = fact.Size == 0 ? entry.Size : fact.Size,
                SavePath = fact.SavePath,
                HasMetadata = true,
                Runtime = fact.Runtime
            };

            result.Add(merged.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(merged)));
        }

        return result;
    }
}
