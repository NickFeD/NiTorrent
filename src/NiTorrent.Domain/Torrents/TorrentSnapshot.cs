namespace NiTorrent.Domain.Torrents;

public sealed record TorrentSnapshot(
    TorrentId Id,
    string Key,
    string Name,
    long Size,
    string SavePath,
    DateTimeOffset AddedAtUtc,
    TorrentStatus Status
);
