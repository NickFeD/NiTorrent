namespace NiTorrent.Domain.Torrents;

public static class DeferredActionPolicy
{
    public static IReadOnlyList<DeferredAction> Merge(IReadOnlyList<DeferredAction> existing, DeferredAction next)
    {
        if (next.Type is DeferredActionType.RemoveKeepData or DeferredActionType.RemoveWithData)
            return new[] { next };

        if (existing.Count == 0)
            return new[] { next };

        var filtered = existing.Where(x => x.Type is not DeferredActionType.RemoveKeepData and not DeferredActionType.RemoveWithData).ToList();
        filtered.RemoveAll(x => x.Type == next.Type);
        filtered.Add(next);
        return filtered.OrderBy(x => x.RequestedAtUtc).ToList();
    }
}
