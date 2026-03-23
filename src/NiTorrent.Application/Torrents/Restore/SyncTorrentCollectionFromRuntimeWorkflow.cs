using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed class SyncTorrentCollectionFromRuntimeWorkflow(
    ITorrentCollectionRepository repository,
    ITorrentRuntimeFactsProvider runtimeFactsProvider)
{
    public async Task<IReadOnlyList<TorrentEntry>> ExecuteAsync(CancellationToken ct = default)
    {
        var entries = await repository.GetAllAsync(ct).ConfigureAwait(false);
        var runtimeFacts = runtimeFactsProvider.GetAll();
        var synced = TorrentCollectionRestorePolicy.ApplyRuntimeFacts(entries, runtimeFacts).ToList();

        foreach (var entry in synced)
            await repository.UpsertAsync(entry, ct).ConfigureAwait(false);

        await repository.SaveAsync(ct).ConfigureAwait(false);
        return synced;
    }
}
