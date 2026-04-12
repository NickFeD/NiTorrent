using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed class StagedTorrentRehydrationWorkflow(
    ITorrentCollectionRepository repository,
    ITorrentSourceStore sourceStore,
    ITorrentWriteService writeService,
    ILogger<StagedTorrentRehydrationWorkflow> logger)
{
    private const string MissingSourceError = "Не удалось восстановить торрент: отсутствует сохраненный source.";
    private const string RehydrateFailedError = "Не удалось восстановить торрент после запуска приложения.";

    public async Task<IReadOnlyList<TorrentEntry>> ExecuteAsync(IReadOnlyList<TorrentEntry> entries, CancellationToken ct = default)
    {
        var ordered = entries
            .OrderByDescending(x => x.Intent == TorrentIntent.Running)
            .ThenBy(x => x.AddedAtUtc)
            .ToList();

        var updatedEntries = new List<TorrentEntry>(ordered.Count);

        foreach (var entry in ordered)
        {
            ct.ThrowIfCancellationRequested();

            var sourceBytes = await sourceStore.TryLoadAsync(entry.Id, entry.Key, ct).ConfigureAwait(false);
            if (sourceBytes is null || sourceBytes.Length == 0)
            {
                logger.LogWarning("Missing persisted source for torrent {TorrentId}", entry.Id.Value);
                var failed = entry.WithRuntime(new TorrentRuntimeStateOld(
                    TorrentLifecycleStateOld.Error,
                    entry.Runtime.IsComplete,
                    entry.Runtime.Progress,
                    0,
                    0,
                    MissingSourceError,
                    false));

                await repository.UpsertAsync(failed, ct).ConfigureAwait(false);
                updatedEntries.Add(failed);
                continue;
            }

            try
            {
                var runtime = await writeService.RehydrateAsync(entry, sourceBytes, ct).ConfigureAwait(false);
                var updated = entry.WithRuntime(runtime);
                await repository.UpsertAsync(updated, ct).ConfigureAwait(false);
                updatedEntries.Add(updated);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Failed to rehydrate torrent {TorrentId}", entry.Id.Value);
                var failed = entry.WithRuntime(new TorrentRuntimeStateOld(
                    TorrentLifecycleStateOld.Error,
                    entry.Runtime.IsComplete,
                    entry.Runtime.Progress,
                    0,
                    0,
                    RehydrateFailedError,
                    false));

                await repository.UpsertAsync(failed, ct).ConfigureAwait(false);
                updatedEntries.Add(failed);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Failed to rehydrate torrent {TorrentId}", entry.Id.Value);
                var failed = entry.WithRuntime(new TorrentRuntimeStateOld(
                    TorrentLifecycleStateOld.Error,
                    entry.Runtime.IsComplete,
                    entry.Runtime.Progress,
                    0,
                    0,
                    RehydrateFailedError,
                    false));

                await repository.UpsertAsync(failed, ct).ConfigureAwait(false);
                updatedEntries.Add(failed);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Failed to rehydrate torrent {TorrentId}", entry.Id.Value);
                var failed = entry.WithRuntime(new TorrentRuntimeStateOld(
                    TorrentLifecycleStateOld.Error,
                    entry.Runtime.IsComplete,
                    entry.Runtime.Progress,
                    0,
                    0,
                    RehydrateFailedError,
                    false));

                await repository.UpsertAsync(failed, ct).ConfigureAwait(false);
                updatedEntries.Add(failed);
            }
        }

        await repository.SaveAsync(ct).ConfigureAwait(false);
        return updatedEntries;
    }
}
