namespace NiTorrent.Domain.Torrents;

public enum TorrentStatusSource
{
    Live = 0,
    Cached = 1
}

public sealed record TorrentStatus(
    TorrentLifecycleStateOld Phase,
    bool IsComplete,
    double Progress,
    long DownloadRateBytesPerSecond,
    long UploadRateBytesPerSecond,
    string? Error = null,
    TorrentStatusSource Source = TorrentStatusSource.Live
);
