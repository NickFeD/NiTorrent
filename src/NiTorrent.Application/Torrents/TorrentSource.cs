namespace NiTorrent.Application.Torrents;

public abstract record TorrentSource
{
    public sealed record Magnet(string Uri) : TorrentSource;
    public sealed record TorrentFile(string path) : TorrentSource;
}
