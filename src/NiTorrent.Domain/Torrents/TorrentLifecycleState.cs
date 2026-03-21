namespace NiTorrent.Domain.Torrents;

public enum TorrentLifecycleState
{
    Unknown = 0,
    WaitingForEngine = 1,
    EngineStarting = 2,
    FetchingMetadata = 3,
    Checking = 4,
    Downloading = 5,
    Seeding = 6,
    Paused = 7,
    Stopped = 8,
    Error = 9,
    Removed = 10
}
