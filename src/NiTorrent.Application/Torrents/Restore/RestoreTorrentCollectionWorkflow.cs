using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Deferred;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed class RestoreTorrentCollectionWorkflow : IRestoreTorrentCollectionWorkflow
{
    private readonly ITorrentCollectionRepository _repository;
    private readonly ITorrentEngineLifecycle _engineLifecycle;
    private readonly ITorrentRuntimeFactsProvider _runtimeFactsProvider;
    private readonly IApplyDeferredTorrentActionsWorkflow _applyDeferredActionsWorkflow;

    public RestoreTorrentCollectionWorkflow(
        ITorrentCollectionRepository repository,
        ITorrentEngineLifecycle engineLifecycle,
        ITorrentRuntimeFactsProvider runtimeFactsProvider,
        IApplyDeferredTorrentActionsWorkflow applyDeferredActionsWorkflow)
    {
        _repository = repository;
        _engineLifecycle = engineLifecycle;
        _runtimeFactsProvider = runtimeFactsProvider;
        _applyDeferredActionsWorkflow = applyDeferredActionsWorkflow;
    }

    public async Task<RestoreTorrentCollectionResult> ExecuteAsync(CancellationToken ct = default)
    {
        var earlyCollection = await _repository.GetAllAsync(ct).ConfigureAwait(false);

        await _engineLifecycle.InitializeAsync(ct).ConfigureAwait(false);
        var runtimeFacts = _runtimeFactsProvider.GetAll();

        var syncedCollection = TorrentCollectionRestorePolicy.ApplyRuntimeFacts(earlyCollection, runtimeFacts).ToList();
        var executionPlan = BuildExecutionPlan(syncedCollection);

        var deferredResult = await _applyDeferredActionsWorkflow.ExecuteAsync(executionPlan, ct).ConfigureAwait(false);
        syncedCollection = deferredResult.UpdatedEntries.ToList();
        if (deferredResult.RemovedIds.Count > 0)
        {
            syncedCollection.RemoveAll(x => deferredResult.RemovedIds.Contains(x.Id));
        }

        foreach (var entry in syncedCollection)
        {
            await _repository.UpsertAsync(entry, ct).ConfigureAwait(false);
        }
        await _repository.SaveAsync(ct).ConfigureAwait(false);

        return new RestoreTorrentCollectionResult(earlyCollection, syncedCollection, runtimeFacts);
    }

    private static IReadOnlyList<TorrentEntry> BuildExecutionPlan(IReadOnlyList<TorrentEntry> entries)
    {
        var now = DateTimeOffset.UtcNow;
        return entries.Select(entry =>
        {
            var planned = entry;

            if (entry.Intent == TorrentIntent.Running)
            {
                planned = planned.WithDeferredActions(
                    DeferredActionPolicy.Merge(planned.DeferredActions, new DeferredAction(DeferredActionType.Start, now)));
            }
            else if (entry.Intent == TorrentIntent.Paused)
            {
                planned = planned.WithDeferredActions(
                    DeferredActionPolicy.Merge(planned.DeferredActions, new DeferredAction(DeferredActionType.Pause, now)));
            }

            return planned;
        }).ToList();
    }
}
