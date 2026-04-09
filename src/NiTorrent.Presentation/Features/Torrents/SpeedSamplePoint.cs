namespace NiTorrent.Presentation.Features.Torrents;

public readonly record struct SpeedSamplePoint(
    long DownloadRateBytesPerSecond,
    long UploadRateBytesPerSecond);
