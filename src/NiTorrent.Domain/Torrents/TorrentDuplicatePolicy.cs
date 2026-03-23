namespace NiTorrent.Domain.Torrents;

public static class TorrentDuplicatePolicy
{
    public static TorrentEntry? FindDuplicate(IEnumerable<TorrentEntry> existing, TorrentKey key, string name, string savePath)
    {
        if (!key.IsEmpty)
        {
            var byKey = existing.FirstOrDefault(x => !x.Key.IsEmpty && string.Equals(x.Key.Value, key.Value, StringComparison.OrdinalIgnoreCase));
            if (byKey is not null)
                return byKey;
        }

        return existing.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SavePath, savePath, StringComparison.OrdinalIgnoreCase));
    }
}
