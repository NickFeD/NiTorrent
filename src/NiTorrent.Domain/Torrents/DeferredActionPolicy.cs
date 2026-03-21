namespace NiTorrent.Domain.Torrents;

public static class DeferredActionPolicy
{
    public static DeferredAction? BuildForIntent(TorrentIntent intent, DateTimeOffset now)
        => intent switch
        {
            TorrentIntent.Start => new DeferredAction(DeferredActionType.Start, now),
            TorrentIntent.Pause => new DeferredAction(DeferredActionType.Pause, now),
            _ => null
        };
}
