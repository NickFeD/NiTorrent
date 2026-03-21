namespace NiTorrent.Domain.Torrents;

public static class TorrentStatusResolver
{
    public static TorrentLifecycleState ResolveVisibleState(TorrentIntent intent, TorrentRuntimeState runtime)
    {
        if (intent == TorrentIntent.Pause)
            return TorrentLifecycleState.Paused;

        return runtime.Lifecycle switch
        {
            TorrentLifecycleState.Paused or TorrentLifecycleState.Stopped => TorrentLifecycleState.WaitingForEngine,
            _ => runtime.Lifecycle
        };
    }
}
