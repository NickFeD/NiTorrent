namespace NiTorrent.Domain.Torrents;

public enum TorrentSnapshotSource
{
    Live = 0,
    Cached = 1
}

public sealed record TorrentStatus(
    TorrentPhase Phase,
    bool IsComplete,
    double Progress,
    long DownloadRateBytesPerSecond,
    long UploadRateBytesPerSecond,
    string? Error = null,
    TorrentSnapshotSource Source = TorrentSnapshotSource.Live
);
