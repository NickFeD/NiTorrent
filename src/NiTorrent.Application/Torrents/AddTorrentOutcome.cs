namespace NiTorrent.Application.Torrents;

public enum AddTorrentOutcome
{
    Success = 0,
    AlreadyExists = 1,
    InvalidInput = 2,
    StorageError = 3
}
