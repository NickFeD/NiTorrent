namespace NiTorrent.Application.Torrents;

public sealed record TorrentPreview(
    string Name,
    long TotalSize,
    IReadOnlyList<TorrentFileEntry> Files
);
