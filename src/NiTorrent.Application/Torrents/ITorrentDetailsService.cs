using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentDetailsService
{
    TorrentDetailsReadModel? Get(TorrentId torrentId);
    TorrentEntrySettings GetSettings(TorrentId torrentId);
    void SaveSettings(TorrentId torrentId, TorrentEntrySettings settings);
}
