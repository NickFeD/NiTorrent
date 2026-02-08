namespace NiTorrent.Application.Torrents;

public abstract record TorrentSource
{
    public sealed record Magnet(string Uri) : TorrentSource;
    public sealed record TorrentFile(string Path) : TorrentSource;
    public sealed record TorrentBytes(byte[] Bytes) : TorrentSource;
}
