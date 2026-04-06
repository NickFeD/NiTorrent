using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Deferred;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed class RestoreTorrentCollectionWorkflow(
    ITorrentCollectionRepository repository,
    ITorrentEngineLifecycle engineLifecycle,
    ITorrentWriteService writeService,
    StagedTorrentRehydrationWorkflow stagedRehydrationWorkflow,
    SyncTorrentCollectionFromRuntimeWorkflow syncRuntimeWorkflow,
    ReplayDeferredTorrentActionsWorkflow replayDeferredActionsWorkflow,
    ILegacyTorrentEntrySettingsMigrationSource legacySettingsRepository) : IRestoreTorrentCollectionWorkflow
{
    public async Task<RestoreTorrentCollectionResult> ExecuteAsync(CancellationToken ct = default)
    {
        await MigrateLegacyPerTorrentSettingsAsync(ct).ConfigureAwait(false);
        var earlyCollection = await repository.GetAllAsync(ct).ConfigureAwait(false);

        await engineLifecycle.InitializeAsync(ct).ConfigureAwait(false);
        await writeService.ApplySettingsAsync(ct).ConfigureAwait(false);
        await stagedRehydrationWorkflow.ExecuteAsync(earlyCollection, ct).ConfigureAwait(false);

        var syncResult = await syncRuntimeWorkflow.ExecuteAsync(ct).ConfigureAwait(false);
        var syncedCollection = syncResult.Entries.ToList();

        var executionPlan = BuildExecutionPlan(syncedCollection);

        var deferredResult = await replayDeferredActionsWorkflow
            .ExecuteAsync(executionPlan, trigger: "restore-startup", ct: ct)
            .ConfigureAwait(false);
        syncedCollection = deferredResult.UpdatedEntries.ToList();
        if (deferredResult.RemovedIds.Count > 0)
            syncedCollection.RemoveAll(x => deferredResult.RemovedIds.Contains(x.Id));

        foreach (var entry in syncedCollection)
            await repository.UpsertAsync(entry, ct).ConfigureAwait(false);

        await repository.SaveAsync(ct: ct).ConfigureAwait(false);
        return new RestoreTorrentCollectionResult(earlyCollection, syncedCollection, syncResult.RuntimeFacts);
    }

    private async Task MigrateLegacyPerTorrentSettingsAsync(CancellationToken ct)
    {
        var entries = await repository.GetAllAsync(ct).ConfigureAwait(false);
        var changed = false;

        foreach (var entry in entries)
        {
            var legacySettings = legacySettingsRepository.Load(entry.Id);
            var hasLegacySettings = !legacySettings.IsDefault();

            if (entry.PerTorrentSettings is null && hasLegacySettings)
            {
                await repository.UpsertAsync(entry.WithPerTorrentSettings(legacySettings), ct).ConfigureAwait(false);
                changed = true;
            }

            if (hasLegacySettings)
                legacySettingsRepository.Remove(entry.Id);
        }

        if (changed)
            await repository.SaveAsync(ct: ct).ConfigureAwait(false);
    }

    private static List<TorrentEntry> BuildExecutionPlan(List<TorrentEntry> entries)
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            .. entries.Select(entry =>
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
            })
        ];
    }
}
