namespace NiTorrent.Domain.Torrents;

public readonly record struct TorrentId(Guid Value)
{
    public static TorrentId New() => new(Guid.NewGuid());
    public static TorrentId Empty => new(Guid.Empty);

    public override string ToString() => Value.ToString("D");
}
