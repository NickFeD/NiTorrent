namespace NiTorrent.Domain.Torrents;

public sealed record TorrentStatus(
    TorrentPhase Phase,
    bool IsComplete,
    double Progress,
    long DownloadRateBytesPerSecond,
    long UploadRateBytesPerSecond,
    string? ErrorMessage = null
);
