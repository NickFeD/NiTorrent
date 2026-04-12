namespace NiTorrent.Domain.Torrents;

public class TorrentLifecycleStateOld
{
    public static object Downloading;

    public static object Error { get; set; }
    public static object FetchingMetadata { get; set; }
    public static object Checking { get; set; }
    public static object Seeding { get; set; }
    public static object Paused { get; set; }
    public static object Stopped { get; set; }
    public static object Unknown { get; set; }
    public static TorrentLifecycleStateOld WaitingForEngine { get; set; }
    public TorrentLifecycleStateOld LifecycleState { get; internal set; }

}
