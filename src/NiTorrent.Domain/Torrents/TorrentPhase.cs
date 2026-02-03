namespace NiTorrent.Domain.Torrents;

public enum TorrentPhase
{
    Unknown = 0,

    FetchingMetadata,
    Checking,

    Downloading,
    Seeding,

    Paused,
    Stopped,

    Error
}

