namespace NiTorrent.Domain.Torrents;

public static class TorrentStatusResolver
{
    public static TorrentRuntimeState ResolveExpectedRuntime(TorrentEntry entry)
    {
        if (entry.Intent == TorrentIntent.Removed)
        {
            return entry.Runtime with
            {
                LifecycleState = TorrentLifecycleState.Stopped,
                DownloadRateBytesPerSecond = 0,
                UploadRateBytesPerSecond = 0,
                Error = null,
                IsEngineBacked = false
            };
        }

        if (entry.Intent == TorrentIntent.Paused)
        {
            return entry.Runtime with
            {
                LifecycleState = TorrentLifecycleState.Paused,
                DownloadRateBytesPerSecond = 0,
                UploadRateBytesPerSecond = 0,
                IsEngineBacked = false
            };
        }

        if (entry.Runtime.IsEngineBacked)
            return entry.Runtime;

        return entry.Runtime with
        {
            LifecycleState = entry.Runtime.LifecycleState is TorrentLifecycleState.Unknown or TorrentLifecycleState.Stopped or TorrentLifecycleState.Paused
                ? TorrentLifecycleState.WaitingForEngine
                : entry.Runtime.LifecycleState,
            IsEngineBacked = false,
            DownloadRateBytesPerSecond = 0,
            UploadRateBytesPerSecond = 0
        };
    }
}
