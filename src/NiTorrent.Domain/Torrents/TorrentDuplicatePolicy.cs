namespace NiTorrent.Domain.Torrents;

public static class TorrentDuplicatePolicy
{
    public static TorrentEntry? FindDuplicateByKey(IEnumerable<TorrentEntry> existing, TorrentKey key)
    {
        if (key.IsEmpty)
            return null;

        return existing.FirstOrDefault(x =>
            !x.Key.IsEmpty &&
            string.Equals(x.Key.Value, key.Value, StringComparison.OrdinalIgnoreCase));
    }

    public static TorrentEntry? FindDuplicate(IEnumerable<TorrentEntry> existing, TorrentKey key, string name, SavePath savePath)
    {
        var byKey = FindDuplicateByKey(existing, key);
        if (byKey is not null)
            return byKey;

        return existing.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SavePath.Value, savePath.Value, StringComparison.OrdinalIgnoreCase));
    }
}
