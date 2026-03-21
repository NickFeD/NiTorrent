using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentEntrySettingsRepository
{
    TorrentEntrySettings Load(TorrentId torrentId);
    void Save(TorrentId torrentId, TorrentEntrySettings settings);
    void Remove(TorrentId torrentId);
}
