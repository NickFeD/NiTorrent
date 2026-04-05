using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Deferred;

public sealed class ReplayDeferredTorrentActionsWorkflow(
    ITorrentCollectionRepository repository,
    IApplyDeferredTorrentActionsWorkflow applyDeferredActionsWorkflow)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ApplyDeferredTorrentActionsResult> ExecuteAsync(
        IReadOnlyList<TorrentEntry>? entries = null,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var sourceEntries = entries ?? await repository.GetAllAsync(ct).ConfigureAwait(false);

            if (sourceEntries.All(x => x.DeferredActions.Count == 0))
                return new ApplyDeferredTorrentActionsResult(sourceEntries.ToList(), [], [], []);

            var result = await applyDeferredActionsWorkflow.ExecuteAsync(sourceEntries, ct).ConfigureAwait(false);

            foreach (var entry in result.UpdatedEntries)
                await repository.UpsertAsync(entry, ct).ConfigureAwait(false);

            foreach (var removedId in result.RemovedIds)
                await repository.RemoveAsync(removedId, ct).ConfigureAwait(false);

            await repository.SaveAsync(ct).ConfigureAwait(false);

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
