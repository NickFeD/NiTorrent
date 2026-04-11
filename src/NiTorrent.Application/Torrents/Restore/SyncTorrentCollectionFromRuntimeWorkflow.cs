using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed record SyncTorrentCollectionFromRuntimeResult(
    IReadOnlyList<TorrentEntry> Entries,
    IReadOnlyList<TorrentRuntimeFact> RuntimeFacts);

public sealed class SyncTorrentCollectionFromRuntimeWorkflow(
    ITorrentCollectionRepository repository,
    ITorrentRuntimeFactsProvider runtimeFactsProvider) : ISyncTorrentCollectionFromRuntimeWorkflow
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

            var originalById = entries.ToDictionary(x => x.Id, x => x);
            var hasDurableChanges = false;
            foreach (var entry in synced)
            {
                if (originalById.TryGetValue(entry.Id, out var original) && AreEquivalent(original, entry))
                    continue;

                hasDurableChanges |= !originalById.TryGetValue(entry.Id, out original) || HasDurableDifference(original, entry);
                await repository.UpsertAsync(entry, ct).ConfigureAwait(false);
            }

            // Runtime sync is high-frequency. Persist only durable catalog changes;
            // volatile runtime telemetry is intentionally not written every cycle.
            if (hasDurableChanges)
                await repository.SaveAsync(ct).ConfigureAwait(false);

            return new SyncTorrentCollectionFromRuntimeResult(synced, runtimeFacts);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool AreEquivalent(TorrentEntry left, TorrentEntry right)
    {
        if (left.Id != right.Id
            || left.Key != right.Key
            || !string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            || left.Size != right.Size
            || !string.Equals(left.SavePath, right.SavePath, StringComparison.Ordinal)
            || left.AddedAtUtc != right.AddedAtUtc
            || left.Intent != right.Intent
            || left.LifecycleState != right.LifecycleState
            || left.HasMetadata != right.HasMetadata
            || !Equals(left.Runtime, right.Runtime)
            || !Equals(left.LastKnownStatus, right.LastKnownStatus))
        {
            return false;
        }

        if (!SequenceEqual(left.SelectedFiles, right.SelectedFiles, StringComparer.OrdinalIgnoreCase))
            return false;

        if (!SequenceEqual(left.DeferredActions, right.DeferredActions, EqualityComparer<DeferredAction>.Default))
            return false;

        return SettingsEqual(left.PerTorrentSettings, right.PerTorrentSettings);
    }

    private static bool SettingsEqual(TorrentEntrySettings? left, TorrentEntrySettings? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return string.Equals(left.DownloadPathOverride, right.DownloadPathOverride, StringComparison.Ordinal)
               && left.MaximumDownloadRateBytesPerSecond == right.MaximumDownloadRateBytesPerSecond
               && left.MaximumUploadRateBytesPerSecond == right.MaximumUploadRateBytesPerSecond
               && left.SequentialDownload == right.SequentialDownload;
    }

    private static bool HasDurableDifference(TorrentEntry left, TorrentEntry right)
    {
        if (left.Id != right.Id
            || left.Key != right.Key
            || !string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            || left.Size != right.Size
            || !string.Equals(left.SavePath, right.SavePath, StringComparison.Ordinal)
            || left.AddedAtUtc != right.AddedAtUtc
            || left.Intent != right.Intent
            || left.HasMetadata != right.HasMetadata)
        {
            return true;
        }

        if (!SequenceEqual(left.SelectedFiles, right.SelectedFiles, StringComparer.OrdinalIgnoreCase))
            return true;

        if (!SequenceEqual(left.DeferredActions, right.DeferredActions, EqualityComparer<DeferredAction>.Default))
            return true;

        return !SettingsEqual(left.PerTorrentSettings, right.PerTorrentSettings);
    }

    private static bool SequenceEqual<T>(
        IReadOnlyList<T> left,
        IReadOnlyList<T> right,
        IEqualityComparer<T> comparer)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!comparer.Equals(left[i], right[i]))
                return false;
        }

        return true;
    }
}
