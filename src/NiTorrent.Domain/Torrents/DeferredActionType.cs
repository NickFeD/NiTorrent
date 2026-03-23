namespace NiTorrent.Domain.Torrents;

public enum DeferredActionType
{
    Start = 0,
    Pause = 1,
    RemoveKeepData = 2,
    RemoveWithData = 3
}
