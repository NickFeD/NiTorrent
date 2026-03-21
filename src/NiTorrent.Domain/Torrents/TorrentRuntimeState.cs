namespace NiTorrent.Domain.Torrents;

public sealed record TorrentRuntimeState(
    TorrentPhase Phase,
    bool IsComplete,
    double Progress,
    long DownloadRateBytesPerSecond,
    long UploadRateBytesPerSecond,
    string? Error = null,
    bool IsAvailable = true)
{
    public static TorrentRuntimeState Unavailable { get; } = new(
        TorrentPhase.Unknown,
        IsComplete: false,
        Progress: 0,
        DownloadRateBytesPerSecond: 0,
        UploadRateBytesPerSecond: 0,
        Error: null,
        IsAvailable: false);

    public bool IsActive => Phase is TorrentPhase.Downloading or TorrentPhase.Seeding or TorrentPhase.Checking or TorrentPhase.FetchingMetadata;
}
