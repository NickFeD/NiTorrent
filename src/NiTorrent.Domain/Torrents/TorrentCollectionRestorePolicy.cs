namespace NiTorrent.Domain.Torrents;

public static class TorrentCollectionRestorePolicy
{
    public static IReadOnlyList<TorrentEntry> ApplyRuntimeFacts(
        IReadOnlyList<TorrentEntry> entries,
        IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
    {
        var matches = TorrentRuntimeFactMatcher.MatchEntries(entries, runtimeFacts);

        var result = new List<TorrentEntry>(entries.Count);
        foreach (var entry in entries)
        {
            matches.TryGetValue(entry.Id, out var fact);

            if (fact is null)
            {
                result.Add(entry.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(entry)));
                continue;
            }

            var updated = entry.WithRuntime(fact.Runtime) with
            {
                Key = fact.Key.IsEmpty ? entry.Key : fact.Key,
                Name = string.IsNullOrWhiteSpace(fact.Name) ? entry.Name : fact.Name,
                Size = fact.Size == 0 ? entry.Size : fact.Size,
                SavePath = string.IsNullOrWhiteSpace(fact.SavePath) ? entry.SavePath : fact.SavePath,
                HasMetadata = true
            };

            result.Add(updated);
        }

        return result;
    }
}
