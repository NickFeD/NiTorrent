namespace NiTorrent.Domain.Torrents;

public sealed record TorrentRuntimeState(
    TorrentLifecycleState LifecycleState,
    bool IsComplete,
    TorrentProgress Progress,
    TransferSpeed DownloadRateBytesPerSecond,
    TransferSpeed UploadRateBytesPerSecond,
    string? Error,
    bool IsEngineBacked)
{
    public static TorrentRuntimeState WaitingForEngine(double progress, bool isComplete) =>
        new(TorrentLifecycleState.WaitingForEngine, isComplete, progress, 0, 0, null, false);
}
