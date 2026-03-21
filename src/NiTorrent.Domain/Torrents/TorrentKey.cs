namespace NiTorrent.Domain.Torrents;

public readonly record struct TorrentKey
{
    public string Value { get; }

    public TorrentKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Torrent key cannot be empty.", nameof(value));
        }

        Value = Normalize(value);
    }

    public static bool TryCreate(string? value, out TorrentKey key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            key = default;
            return false;
        }

        key = new TorrentKey(value);
        return true;
    }

    public static TorrentKey Parse(string value) => new(value);

    public override string ToString() => Value;

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
