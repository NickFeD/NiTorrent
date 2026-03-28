using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class UpdatePerTorrentSettingsWorkflow(
    ITorrentEntrySettingsRuntimeApplier runtimeApplier,
    NiTorrent.Application.Abstractions.ITorrentCollectionRepository collectionRepository)
{
    public async Task ExecuteAsync(TorrentId torrentId, TorrentEntrySettings settings, CancellationToken ct = default)
    {
        var entry = await collectionRepository.TryGetAsync(torrentId, ct).ConfigureAwait(false);
        if (entry is null)
            throw new InvalidOperationException($"Torrent not found: {torrentId.Value}");

        var effectiveSettings = settings.IsDefault() ? null : settings;
        await collectionRepository.UpsertAsync(entry.WithPerTorrentSettings(effectiveSettings), ct).ConfigureAwait(false);
        await collectionRepository.SaveAsync(ct).ConfigureAwait(false);

        await runtimeApplier.ApplyAsync(torrentId, effectiveSettings ?? TorrentEntrySettings.Default, ct).ConfigureAwait(false);
    }
}
