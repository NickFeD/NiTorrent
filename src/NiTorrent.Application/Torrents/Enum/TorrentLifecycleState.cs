namespace NiTorrent.Application.Torrents.Enum;

public enum TorrentLifecycleState
{
    Unknown = 0,
    Checking,
    Moving,
    FetchingMetadata = 3,
    Stalled = 4,
    Downloading = 5,
    Seeding = 6,
    Paused = 7,
    Stopped = 8,
    Error = 9,
    Removed = 10
}

//
//WaitingForEngine = 1,
//EngineStarting = 2,
