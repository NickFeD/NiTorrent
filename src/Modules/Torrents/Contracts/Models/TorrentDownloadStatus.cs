namespace NiTorrent.Domain.Torrents;

public enum TorrentDownloadStatus
{
    Created = 0,
    Running = 1,
    Paused = 2,
    Completed = 3,
    Failed = 4,
    Deleted = 5
}
