namespace NiTorrent.Domain.Torrents;

public sealed record TorrentStatus(
    TorrentPhase Phase,
    bool IsComplete,
    double Progress,
    long DownloadRateBytesPerSecond,
    long UploadRateBytesPerSecond,
    string? ErrorMessage
);

public sealed record TorrentSnapshot(
    TorrentId Id,
    string Key,
    string Name,
    string SavePath,
    DateTimeOffset AddedAtUtc,
    TorrentStatus Status
);
