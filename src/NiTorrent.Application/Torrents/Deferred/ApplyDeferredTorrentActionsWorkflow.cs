using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Deferred;

public sealed class ApplyDeferredTorrentActionsWorkflow(
    ITorrentEngineGateway engineGateway) : IApplyDeferredTorrentActionsWorkflow
{
    public async Task<ApplyDeferredTorrentActionsResult> ExecuteAsync(IReadOnlyList<TorrentEntry> entries, CancellationToken ct = default)
    {
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

            var current = entry;
            var pendingActions = current.DeferredActions.OrderBy(x => x.RequestedAtUtc).ToList();
            var removed = false;

            while (pendingActions.Count > 0)
            {
                var action = pendingActions[0];
                var applied = await TryApplyAsync(current.Id, action, engineGateway, ct).ConfigureAwait(false);
                if (!applied)
                {
                    deferredIds.Add(current.Id);
                    break;
                }

                pendingActions.RemoveAt(0);
                appliedIds.Add(current.Id);

                switch (action.Type)
                {
                    case DeferredActionType.Start:
                        current = current.WithIntent(TorrentIntent.Running);
                        break;
                    case DeferredActionType.Pause:
                        current = current.WithIntent(TorrentIntent.Paused);
                        break;
                    case DeferredActionType.RemoveKeepData:
                    case DeferredActionType.RemoveWithData:
                        removedIds.Add(current.Id);
                        removed = true;
                        break;
                }

                if (removed)
                    break;
            }

            if (removed)
                continue;

            current = current.WithDeferredActions(pendingActions);
            current = current.WithRuntime(TorrentStatusResolver.ResolveExpectedRuntime(current));
            updated.Add(current);
        }

        return new ApplyDeferredTorrentActionsResult(updated, removedIds, appliedIds, deferredIds);
    }

    private static async Task<bool> TryApplyAsync(TorrentId id, DeferredAction action, ITorrentEngineGateway engineGateway, CancellationToken ct)
    {
        try
        {
            return action.Type switch
            {
                DeferredActionType.Start => await engineGateway.StartAsync(id, ct).ConfigureAwait(false),
                DeferredActionType.Pause => await engineGateway.PauseAsync(id, ct).ConfigureAwait(false),
                DeferredActionType.RemoveKeepData => await engineGateway.RemoveAsync(id, deleteData: false, ct).ConfigureAwait(false),
                DeferredActionType.RemoveWithData => await engineGateway.RemoveAsync(id, deleteData: true, ct).ConfigureAwait(false),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}
