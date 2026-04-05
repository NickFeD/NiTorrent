using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Deferred;

public sealed class ReplayDeferredTorrentActionsWorkflow(
    ITorrentCollectionRepository repository,
    IApplyDeferredTorrentActionsWorkflow applyDeferredActionsWorkflow,
    ILogger<ReplayDeferredTorrentActionsWorkflow> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _cycleId;

    public async Task<ApplyDeferredTorrentActionsResult> ExecuteAsync(
        IReadOnlyList<TorrentEntry>? entries = null,
        string trigger = "unspecified",
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var sourceEntries = entries ?? await repository.GetAllAsync(ct).ConfigureAwait(false);
            var normalizedEntries = NormalizeDeferredActions(sourceEntries);
            var cycleId = Interlocked.Increment(ref _cycleId);
            var pendingEntryCount = normalizedEntries.Count(x => x.DeferredActions.Count > 0);
            var pendingActionCount = normalizedEntries.Sum(x => x.DeferredActions.Count);

            logger.LogInformation(
                "Deferred replay cycle {CycleId} started (trigger={Trigger}, pendingEntries={PendingEntries}, pendingActions={PendingActions})",
                cycleId,
                trigger,
                pendingEntryCount,
                pendingActionCount);

            if (pendingActionCount == 0)
            {
                var emptyResult = new ApplyDeferredTorrentActionsResult(normalizedEntries.ToList(), [], [], []);
                logger.LogInformation(
                    "Deferred replay cycle {CycleId} finished (trigger={Trigger}, applied=0, deferred=0, removed=0)",
                    cycleId,
                    trigger);
                return emptyResult;
            }

            var result = await applyDeferredActionsWorkflow.ExecuteAsync(normalizedEntries, ct).ConfigureAwait(false);

            foreach (var entry in result.UpdatedEntries)
                await repository.UpsertAsync(entry, ct).ConfigureAwait(false);

            foreach (var removedId in result.RemovedIds)
                await repository.RemoveAsync(removedId, ct).ConfigureAwait(false);

            await repository.SaveAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "Deferred replay cycle {CycleId} finished (trigger={Trigger}, applied={Applied}, deferred={Deferred}, removed={Removed})",
                cycleId,
                trigger,
                result.AppliedIds.Count,
                result.DeferredIds.Count,
                result.RemovedIds.Count);

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IReadOnlyList<TorrentEntry> NormalizeDeferredActions(IReadOnlyList<TorrentEntry> entries)
    {
        var normalized = new List<TorrentEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.DeferredActions.Count <= 1)
            {
                normalized.Add(entry);
                continue;
            }

            IReadOnlyList<DeferredAction> merged = Array.Empty<DeferredAction>();
            foreach (var action in entry.DeferredActions.OrderBy(x => x.RequestedAtUtc))
                merged = DeferredActionPolicy.Merge(merged, action);

            normalized.Add(entry.WithDeferredActions(merged));
        }

        return normalized;
    }
}
