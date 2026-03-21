using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Deferred;

public sealed class ApplyDeferredTorrentActionsWorkflow(
    ITorrentEngineGateway engineGateway,
    ITorrentRuntimeFactsProvider runtimeFactsProvider) : IApplyDeferredTorrentActionsWorkflow
{
    public async Task<ApplyDeferredTorrentActionsResult> ExecuteAsync(IReadOnlyList<TorrentEntry> entries, CancellationToken ct = default)
    {
        var runtimeFacts = runtimeFactsProvider.GetAll();
        var updated = new List<TorrentEntry>(entries.Count);
        var removedIds = new List<TorrentId>();
        var appliedIds = new List<TorrentId>();
        var deferredIds = new List<TorrentId>();

        foreach (var entry in entries)
        {
            if (entry.DeferredActions.Count == 0)
            {
                updated.Add(entry);
                continue;
            }

            var engineReadyForEntry = HasRuntimeFact(entry, runtimeFacts);
            if (!engineReadyForEntry)
            {
                updated.Add(entry);
                deferredIds.Add(entry.Id);
                continue;
            }

            var current = entry;
            var removed = false;

            foreach (var action in current.DeferredActions.OrderBy(x => x.RequestedAtUtc))
            {
                switch (action.Type)
                {
                    case DeferredActionType.Start:
                        await engineGateway.StartAsync(current.Id, ct).ConfigureAwait(false);
                        current = current.WithIntent(TorrentIntent.Running);
                        appliedIds.Add(current.Id);
                        break;
                    case DeferredActionType.Pause:
                        await engineGateway.PauseAsync(current.Id, ct).ConfigureAwait(false);
                        current = current.WithIntent(TorrentIntent.Paused);
                        appliedIds.Add(current.Id);
                        break;
                    case DeferredActionType.RemoveKeepData:
                        await engineGateway.RemoveAsync(current.Id, deleteData: false, ct).ConfigureAwait(false);
                        removedIds.Add(current.Id);
                        appliedIds.Add(current.Id);
                        removed = true;
                        break;
                    case DeferredActionType.RemoveWithData:
                        await engineGateway.RemoveAsync(current.Id, deleteData: true, ct).ConfigureAwait(false);
                        removedIds.Add(current.Id);
                        appliedIds.Add(current.Id);
                        removed = true;
                        break;
                }

                if (removed)
                    break;
            }

            if (removed)
                continue;

            current = current.WithDeferredActions(Array.Empty<DeferredAction>())
                .WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(current));
            updated.Add(current);
        }

        return new ApplyDeferredTorrentActionsResult(updated, removedIds, appliedIds, deferredIds);
    }

    private static bool HasRuntimeFact(TorrentEntry entry, IReadOnlyList<TorrentRuntimeFact> facts)
    {
        foreach (var fact in facts)
        {
            if (fact.Id is TorrentId id && id == entry.Id)
                return true;

            if (!entry.Key.IsEmpty && !fact.Key.IsEmpty &&
                string.Equals(entry.Key.Value, fact.Key.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
