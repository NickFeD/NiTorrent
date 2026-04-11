namespace NiTorrent.Domain.Torrents;

public sealed record TorrentFileEntry(
    string FullPath,
    long SizeByte,
    bool IsSelected
);
