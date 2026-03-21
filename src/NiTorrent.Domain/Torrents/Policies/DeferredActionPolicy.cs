namespace NiTorrent.Domain.Torrents;

public static class DeferredActionPolicy
{
    public static IReadOnlyList<DeferredAction> ReplaceWithLatest(IEnumerable<DeferredAction> existing, DeferredAction next)
    {
        if (next.Type == DeferredActionType.Remove)
        {
            return [next];
        }

        var actions = existing.Where(x => x.Type == DeferredActionType.Remove).ToList();
        if (actions.Count > 0)
        {
            return actions;
        }

        return [next];
    }
}
