namespace NiTorrent.Domain.Torrents;

public enum TorrentPhase
{
    Unknown = 0,

    /// <summary>
    /// Torrent engine is starting (UI can show cached items, but real download hasn't begun yet).
    /// </summary>
    EngineStarting,

    /// <summary>
    /// User requested an action (e.g. Start), but the engine is not ready yet.
    /// </summary>
    WaitingForEngine,

    FetchingMetadata,
    Checking,

    Downloading,
    Seeding,

    Paused,
    Stopped,

    Error
}

