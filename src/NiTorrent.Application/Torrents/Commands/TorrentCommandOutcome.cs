namespace NiTorrent.Application.Torrents.Commands;

public enum TorrentCommandOutcome
{
    Success = 0,
    Deferred = 1,
    NotFound = 2,
    Failed = 3
}
