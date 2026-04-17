using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.DTo;

public record StoredTorrent(TorrentDownload Torrent, TorrentSource TorrentSource);
