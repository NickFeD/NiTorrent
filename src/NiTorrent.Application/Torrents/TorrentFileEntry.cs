namespace NiTorrent.Application.Torrents;

public sealed record TorrentFileEntry(
    string FullPath,
    long Length,
    bool IsSelected
);
