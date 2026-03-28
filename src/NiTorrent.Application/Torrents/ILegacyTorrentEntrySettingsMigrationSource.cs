using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

/// <summary>
/// One-way migration source for pre-step6 per-torrent settings storage.
/// Not an active repository and not a source of truth after migration.
/// </summary>
public interface ILegacyTorrentEntrySettingsMigrationSource
{
    TorrentEntrySettings Load(TorrentId torrentId);
    void Remove(TorrentId torrentId);
}
