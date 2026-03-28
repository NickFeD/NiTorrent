namespace NiTorrent.Domain.Torrents;

public static class TorrentEntryCommandPolicy
{
    public static TorrentEntry RequestStart(TorrentEntry entry, DateTimeOffset now)
    {
        var updated = entry.WithIntent(TorrentIntent.Running);
        var deferred = DeferredActionPolicy.Merge(ClearExecutionDeferredActions(updated.DeferredActions), new DeferredAction(DeferredActionType.Start, now));

        updated = updated.WithDeferredActions(deferred);
        return updated.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(updated));
    }

    public static TorrentEntry RequestPause(TorrentEntry entry, DateTimeOffset now)
    {
        var updated = entry.WithIntent(TorrentIntent.Paused);
        var deferred = DeferredActionPolicy.Merge(ClearExecutionDeferredActions(updated.DeferredActions), new DeferredAction(DeferredActionType.Pause, now));

        updated = updated.WithDeferredActions(deferred);
        return updated.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(updated));
    }

    public static TorrentEntry RequestRemove(TorrentEntry entry, bool deleteData, DateTimeOffset now)
    {
        var updated = entry.WithIntent(TorrentIntent.Removed);
        var action = new DeferredAction(deleteData ? DeferredActionType.RemoveWithData : DeferredActionType.RemoveKeepData, now);
        updated = updated.WithDeferredActions(DeferredActionPolicy.Merge(Array.Empty<DeferredAction>(), action));
        return updated.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(updated));
    }

    private static IReadOnlyList<DeferredAction> ClearExecutionDeferredActions(IReadOnlyList<DeferredAction> actions) =>
        actions.Where(x => x.Type is not DeferredActionType.Start and not DeferredActionType.Pause)
            .ToList();
}
