namespace NiTorrent.Domain.Torrents;

public enum TorrentLifecycleState
{
    Unknown = 0,
    WaitingForMetadata,
    Ready,
    Running,
    Paused,
    Completed,
    Error,
    Removing
}
