namespace NiTorrent.Domain.Torrents;

public static class TorrentLifecycleStateMapper
{
    public static TorrentLifecycleState FromPhase(TorrentPhase phase) => phase switch
    {
        TorrentPhase.WaitingForEngine => TorrentLifecycleState.WaitingForEngine,
        TorrentPhase.EngineStarting => TorrentLifecycleState.EngineStarting,
        TorrentPhase.FetchingMetadata => TorrentLifecycleState.FetchingMetadata,
        TorrentPhase.Checking => TorrentLifecycleState.Checking,
        TorrentPhase.Downloading => TorrentLifecycleState.Downloading,
        TorrentPhase.Seeding => TorrentLifecycleState.Seeding,
        TorrentPhase.Paused => TorrentLifecycleState.Paused,
        TorrentPhase.Stopped => TorrentLifecycleState.Stopped,
        TorrentPhase.Error => TorrentLifecycleState.Error,
        _ => TorrentLifecycleState.Unknown
    };

    public static TorrentPhase ToPhase(TorrentLifecycleState state) => state switch
    {
        TorrentLifecycleState.WaitingForEngine => TorrentPhase.WaitingForEngine,
        TorrentLifecycleState.EngineStarting => TorrentPhase.EngineStarting,
        TorrentLifecycleState.FetchingMetadata => TorrentPhase.FetchingMetadata,
        TorrentLifecycleState.Checking => TorrentPhase.Checking,
        TorrentLifecycleState.Downloading => TorrentPhase.Downloading,
        TorrentLifecycleState.Seeding => TorrentPhase.Seeding,
        TorrentLifecycleState.Paused => TorrentPhase.Paused,
        TorrentLifecycleState.Stopped => TorrentPhase.Stopped,
        TorrentLifecycleState.Error => TorrentPhase.Error,
        _ => TorrentPhase.Unknown
    };
}
