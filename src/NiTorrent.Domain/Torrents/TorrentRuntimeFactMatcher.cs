namespace NiTorrent.Domain.Torrents;

public static class TorrentRuntimeFactMatcher
{
    public static IReadOnlyDictionary<TorrentId, TorrentRuntimeFact> MatchEntries(
        IReadOnlyList<TorrentEntry> entries,
        IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
    {
        var matches = new Dictionary<TorrentId, TorrentRuntimeFact>();
        if (entries.Count == 0 || runtimeFacts.Count == 0)
            return matches;

        var usedFactIndexes = new HashSet<int>();

        var factIndexesById = runtimeFacts
            .Select((fact, index) => new { fact, index })
            .Where(x => x.fact.Id is not null)
            .GroupBy(x => x.fact.Id!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.index).ToList());

        foreach (var entry in entries)
        {
            if (!factIndexesById.TryGetValue(entry.Id, out var candidates))
                continue;

            var factIndex = candidates.FirstOrDefault(index => !usedFactIndexes.Contains(index), -1);
            if (factIndex < 0)
                continue;

            matches[entry.Id] = runtimeFacts[factIndex];
            usedFactIndexes.Add(factIndex);
        }

        var factIndexesByKey = runtimeFacts
            .Select((fact, index) => new { fact, index })
            .Where(x => !x.fact.Key.IsEmpty)
            .GroupBy(x => x.fact.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.index).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (matches.ContainsKey(entry.Id) || entry.Key.IsEmpty)
                continue;

            if (!factIndexesByKey.TryGetValue(entry.Key.Value, out var candidates))
                continue;

            var factIndex = FindBestCandidateIndex(entry, runtimeFacts, candidates, usedFactIndexes);
            if (factIndex < 0)
                continue;

            matches[entry.Id] = runtimeFacts[factIndex];
            usedFactIndexes.Add(factIndex);
        }

        return matches;
    }

    private static int FindBestCandidateIndex(
        TorrentEntry entry,
        IReadOnlyList<TorrentRuntimeFact> runtimeFacts,
        IReadOnlyList<int> candidateIndexes,
        HashSet<int> usedFactIndexes)
    {
        return FindExactCandidate(entry, runtimeFacts, candidateIndexes, usedFactIndexes)
            ?? FindSavePathCandidate(entry, runtimeFacts, candidateIndexes, usedFactIndexes)
            ?? FindNameCandidate(entry, runtimeFacts, candidateIndexes, usedFactIndexes)
            ?? candidateIndexes.FirstOrDefault(index => !usedFactIndexes.Contains(index), -1);
    }

    private static int? FindExactCandidate(
        TorrentEntry entry,
        IReadOnlyList<TorrentRuntimeFact> runtimeFacts,
        IReadOnlyList<int> candidateIndexes,
        HashSet<int> usedFactIndexes)
        => candidateIndexes.FirstOrDefault(index =>
            !usedFactIndexes.Contains(index)
            && Same(runtimeFacts[index].SavePath, entry.SavePath)
            && Same(runtimeFacts[index].Name, entry.Name), -1) is var match && match >= 0 ? match : null;

    private static int? FindSavePathCandidate(
        TorrentEntry entry,
        IReadOnlyList<TorrentRuntimeFact> runtimeFacts,
        IReadOnlyList<int> candidateIndexes,
        HashSet<int> usedFactIndexes)
        => candidateIndexes.FirstOrDefault(index =>
            !usedFactIndexes.Contains(index)
            && Same(runtimeFacts[index].SavePath, entry.SavePath), -1) is var match && match >= 0 ? match : null;

    private static int? FindNameCandidate(
        TorrentEntry entry,
        IReadOnlyList<TorrentRuntimeFact> runtimeFacts,
        IReadOnlyList<int> candidateIndexes,
        HashSet<int> usedFactIndexes)
        => candidateIndexes.FirstOrDefault(index =>
            !usedFactIndexes.Contains(index)
            && Same(runtimeFacts[index].Name, entry.Name), -1) is var match && match >= 0 ? match : null;

    private static bool Same(string? left, string? right)
        => string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
