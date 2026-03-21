namespace NiTorrent.Domain.Torrents;

public readonly record struct TorrentKey(string Value)
{
    public static TorrentKey Empty => new(string.Empty);
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}
