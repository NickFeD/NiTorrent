using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Commands;

public record StartTorrentDownloadCommand(TorrentSource Source, string DownloadDirectory, List<TorrentFileEntry> Files);
