namespace NiTorrent.Domain.Torrents;

public static class TorrentDuplicatePolicy
{
    public static bool IsDuplicate(TorrentEntry candidate, IEnumerable<TorrentEntry> existing)
        => existing.Any(x => x.Key == candidate.Key);

    public static TorrentEntry? FindDuplicate(TorrentKey key, IEnumerable<TorrentEntry> existing)
        => existing.FirstOrDefault(x => x.Key == key);
}
