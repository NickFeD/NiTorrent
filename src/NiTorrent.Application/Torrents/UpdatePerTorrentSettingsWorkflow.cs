using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class UpdatePerTorrentSettingsWorkflow(
    ITorrentEntrySettingsRuntimeApplier runtimeApplier,
    ITorrentCollectionRepository collectionRepository)
{
    public async Task ExecuteAsync(TorrentId torrentId, TorrentEntrySettings settings, CancellationToken ct = default)
    {
        var entry = await collectionRepository.TryGetAsync(torrentId, ct).ConfigureAwait(false);
        if (entry is null)
            throw new InvalidOperationException($"Torrent not found: {torrentId.Value}");

        var previousSettings = entry.PerTorrentSettings ?? TorrentEntrySettings.Default;
        var effectiveSettings = settings.IsDefault() ? null : settings;
        var runtimeTarget = effectiveSettings ?? TorrentEntrySettings.Default;

        await runtimeApplier.ApplyAsync(torrentId, runtimeTarget, ct).ConfigureAwait(false);

        try
        {
            await collectionRepository.UpsertAsync(entry.WithPerTorrentSettings(effectiveSettings), ct).ConfigureAwait(false);
            await collectionRepository.SaveAsync(ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            await TryRollbackRuntimeAsync(torrentId, previousSettings, ct).ConfigureAwait(false);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            await TryRollbackRuntimeAsync(torrentId, previousSettings, ct).ConfigureAwait(false);
            throw;
        }
        catch (InvalidOperationException)
        {
            await TryRollbackRuntimeAsync(torrentId, previousSettings, ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task TryRollbackRuntimeAsync(TorrentId torrentId, TorrentEntrySettings previousSettings, CancellationToken ct)
    {
        try
        {
            await runtimeApplier.ApplyAsync(torrentId, previousSettings, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Best effort rollback; original persistence error is more actionable.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort rollback; original persistence error is more actionable.
        }
        catch (InvalidOperationException)
        {
            // Best effort rollback; original persistence error is more actionable.
        }
    }
}
