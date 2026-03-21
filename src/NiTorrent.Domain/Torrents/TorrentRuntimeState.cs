namespace NiTorrent.Domain.Torrents;

public sealed record TorrentRuntimeState(
    TorrentLifecycleState Lifecycle,
    bool IsComplete,
    double Progress,
    long DownloadRateBytesPerSecond,
    long UploadRateBytesPerSecond,
    string? Error = null,
    DateTimeOffset? ObservedAtUtc = null
);
