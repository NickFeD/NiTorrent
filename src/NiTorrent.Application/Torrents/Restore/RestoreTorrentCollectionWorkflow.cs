using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Deferred;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed class RestoreTorrentCollectionWorkflow : IRestoreTorrentCollectionWorkflow
{
    private readonly ITorrentCollectionRepository _repository;
    private readonly ITorrentEngineLifecycle _engineLifecycle;
    private readonly SyncTorrentCollectionFromRuntimeWorkflow _syncRuntimeWorkflow;
    private readonly ReplayDeferredTorrentActionsWorkflow _replayDeferredActionsWorkflow;
    private readonly ILegacyTorrentEntrySettingsMigrationSource _legacySettingsRepository;

    public RestoreTorrentCollectionWorkflow(
        ITorrentCollectionRepository repository,
        ITorrentEngineLifecycle engineLifecycle,
        SyncTorrentCollectionFromRuntimeWorkflow syncRuntimeWorkflow,
        ReplayDeferredTorrentActionsWorkflow replayDeferredActionsWorkflow,
        ILegacyTorrentEntrySettingsMigrationSource legacySettingsRepository)
    {
        _repository = repository;
        _engineLifecycle = engineLifecycle;
        _syncRuntimeWorkflow = syncRuntimeWorkflow;
        _replayDeferredActionsWorkflow = replayDeferredActionsWorkflow;
        _legacySettingsRepository = legacySettingsRepository;
    }

    public async Task<RestoreTorrentCollectionResult> ExecuteAsync(CancellationToken ct = default)
    {
        await MigrateLegacyPerTorrentSettingsAsync(ct).ConfigureAwait(false);
        var earlyCollection = await _repository.GetAllAsync(ct).ConfigureAwait(false);

        await _engineLifecycle.InitializeAsync(ct).ConfigureAwait(false);
        var syncResult = await _syncRuntimeWorkflow.ExecuteAsync(ct).ConfigureAwait(false);
        var syncedCollection = syncResult.Entries.ToList();

        var executionPlan = BuildExecutionPlan(syncedCollection);

        var deferredResult = await _replayDeferredActionsWorkflow.ExecuteAsync(executionPlan, ct).ConfigureAwait(false);
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

        return new RestoreTorrentCollectionResult(earlyCollection, syncedCollection, syncResult.RuntimeFacts);
    }


    private async Task MigrateLegacyPerTorrentSettingsAsync(CancellationToken ct)
    {
        var entries = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        var changed = false;

        foreach (var entry in entries)
        {
            var legacySettings = _legacySettingsRepository.Load(entry.Id);
            var hasLegacySettings = !legacySettings.IsDefault();

            if (entry.PerTorrentSettings is null && hasLegacySettings)
            {
                await _repository.UpsertAsync(entry.WithPerTorrentSettings(legacySettings), ct).ConfigureAwait(false);
                changed = true;
            }

            if (hasLegacySettings)
            {
                _legacySettingsRepository.Remove(entry.Id);
            }
        }

        if (changed)
            await _repository.SaveAsync(ct).ConfigureAwait(false);
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
