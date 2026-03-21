namespace NiTorrent.Domain.Torrents;

public static class TorrentStatusResolver
{
    public static TorrentStatus Resolve(TorrentIntent intent, TorrentRuntimeState runtime)
    {
        if (!runtime.IsAvailable)
        {
            return new TorrentStatus(
                intent == TorrentIntent.Run ? TorrentPhase.WaitingForEngine : TorrentPhase.Paused,
                IsComplete: false,
                Progress: runtime.Progress,
                DownloadRateBytesPerSecond: 0,
                UploadRateBytesPerSecond: 0,
                Error: runtime.Error,
                Source: TorrentSnapshotSource.Cached);
        }

        var phase = runtime.Phase;

        if (intent == TorrentIntent.Pause && phase is not TorrentPhase.Error)
        {
            phase = TorrentPhase.Paused;
        }

        if (phase is TorrentPhase.Paused or TorrentPhase.Stopped)
        {
            return new TorrentStatus(
                phase,
                runtime.IsComplete,
                runtime.Progress,
                DownloadRateBytesPerSecond: 0,
                UploadRateBytesPerSecond: 0,
                runtime.Error,
                TorrentSnapshotSource.Live);
        }

        return new TorrentStatus(
            phase,
            runtime.IsComplete,
            runtime.Progress,
            runtime.DownloadRateBytesPerSecond,
            runtime.UploadRateBytesPerSecond,
            runtime.Error,
            TorrentSnapshotSource.Live);
    }
}
