namespace NiTorrent.Domain.Torrents;

public static class TorrentLifecycleStateMapper
{
    public static TorrentLifecycleState FromRuntime(TorrentIntent intent, TorrentRuntimeState runtime)
    {
        if (!runtime.IsAvailable)
        {
            return intent == TorrentIntent.Run
                ? TorrentLifecycleState.Ready
                : TorrentLifecycleState.Paused;
        }

        return runtime.Phase switch
        {
            TorrentPhase.FetchingMetadata => TorrentLifecycleState.WaitingForMetadata,
            TorrentPhase.Checking => TorrentLifecycleState.Running,
            TorrentPhase.Downloading => TorrentLifecycleState.Running,
            TorrentPhase.Seeding => TorrentLifecycleState.Completed,
            TorrentPhase.Paused => TorrentLifecycleState.Paused,
            TorrentPhase.Stopped => intent == TorrentIntent.Run ? TorrentLifecycleState.Ready : TorrentLifecycleState.Paused,
            TorrentPhase.Error => TorrentLifecycleState.Error,
            _ => intent == TorrentIntent.Run ? TorrentLifecycleState.Ready : TorrentLifecycleState.Paused
        };
    }
}
