using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.DTo;

public sealed record TorrentPreview(
    bool AlreadyExists,
    string Name,
    string InfoHash,
    long TotalSize,
    IReadOnlyList<TorrentFileEntry> Files
);
