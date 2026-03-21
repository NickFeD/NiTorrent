namespace NiTorrent.Domain.Torrents;

public enum TorrentLifecycleState
{
    Unknown = 0,
    WaitingForEngine,
    FetchingMetadata,
    Checking,
    Downloading,
    Seeding,
    Paused,
    Stopped,
    Error
}
