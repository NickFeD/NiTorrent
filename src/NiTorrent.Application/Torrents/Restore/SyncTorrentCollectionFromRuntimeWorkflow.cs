using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed record SyncTorrentCollectionFromRuntimeResult(
    IReadOnlyList<TorrentEntry> Entries,
    IReadOnlyList<TorrentRuntimeFact> RuntimeFacts);

public sealed class SyncTorrentCollectionFromRuntimeWorkflow(
    ITorrentCollectionRepository repository,
    ITorrentRuntimeFactsProvider runtimeFactsProvider)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<SyncTorrentCollectionFromRuntimeResult> ExecuteAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = await repository.GetAllAsync(ct).ConfigureAwait(false);
            var runtimeFacts = runtimeFactsProvider.GetAll();
            var synced = TorrentCollectionRestorePolicy.ApplyRuntimeFacts(entries, runtimeFacts).ToList();

            foreach (var entry in synced)
                await repository.UpsertAsync(entry, ct).ConfigureAwait(false);

            await repository.SaveAsync(ct).ConfigureAwait(false);
            return new SyncTorrentCollectionFromRuntimeResult(synced, runtimeFacts);
        }
        finally
        {
            _gate.Release();
        }
    }
}
