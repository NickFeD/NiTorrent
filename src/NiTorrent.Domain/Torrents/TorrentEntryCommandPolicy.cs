namespace NiTorrent.Domain.Torrents;

public static class TorrentEntryCommandPolicy
{
    public static TorrentEntry RequestStart(TorrentEntry entry, DateTimeOffset now, bool engineReady)
    {
        var updated = entry.WithIntent(TorrentIntent.Running);
        var deferred = engineReady
            ? ClearExecutionDeferredActions(updated.DeferredActions)
            : DeferredActionPolicy.Merge(ClearExecutionDeferredActions(updated.DeferredActions), new DeferredAction(DeferredActionType.Start, now));

        updated = updated.WithDeferredActions(deferred);
        return updated.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(updated));
    }

    public static TorrentEntry RequestPause(TorrentEntry entry, DateTimeOffset now, bool engineReady)
    {
        var updated = entry.WithIntent(TorrentIntent.Paused);
        var deferred = engineReady
            ? ClearExecutionDeferredActions(updated.DeferredActions)
            : DeferredActionPolicy.Merge(ClearExecutionDeferredActions(updated.DeferredActions), new DeferredAction(DeferredActionType.Pause, now));

        updated = updated.WithDeferredActions(deferred);
        return updated.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(updated));
    }

    public static TorrentEntry RequestRemove(TorrentEntry entry, bool deleteData, DateTimeOffset now, bool engineReady)
    {
        var updated = entry.WithIntent(TorrentIntent.Removed);
        if (engineReady)
            return updated.WithDeferredActions(Array.Empty<DeferredAction>());

        var action = new DeferredAction(deleteData ? DeferredActionType.RemoveWithData : DeferredActionType.RemoveKeepData, now);
        return updated.WithDeferredActions(DeferredActionPolicy.Merge(Array.Empty<DeferredAction>(), action));
    }

    private static IReadOnlyList<DeferredAction> ClearExecutionDeferredActions(IReadOnlyList<DeferredAction> actions) =>
        actions.Where(x => x.Type is not DeferredActionType.Start and not DeferredActionType.Pause)
            .ToList();
}
