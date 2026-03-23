using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

/// <summary>
/// Synchronizes persisted product-owned torrent entries with the latest runtime facts.
/// This keeps the catalog aligned with engine state without letting infrastructure own
/// product merge rules.
/// </summary>
public sealed class SyncTorrentCollectionFromRuntimeWorkflow(
    ITorrentCollectionRepository repository,
    ITorrentRuntimeFactsProvider runtimeFactsProvider)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var entries = await repository.GetAllAsync(ct).ConfigureAwait(false);
        if (entries.Count == 0)
            return;

        var runtimeFacts = runtimeFactsProvider.GetAll();
        if (runtimeFacts.Count == 0)
            return;

        var synced = TorrentCollectionRestorePolicy.ApplyRuntimeFacts(entries, runtimeFacts);
        if (!HasChanges(entries, synced))
            return;

        foreach (var entry in synced)
        {
            await repository.UpsertAsync(entry, ct).ConfigureAwait(false);
        }

        await repository.SaveAsync(ct).ConfigureAwait(false);
    }

    private static bool HasChanges(IReadOnlyList<TorrentEntry> before, IReadOnlyList<TorrentEntry> after)
    {
        if (before.Count != after.Count)
            return true;

        for (var i = 0; i < before.Count; i++)
        {
            if (!Equals(before[i], after[i]))
                return true;
        }

        return false;
    }
}
