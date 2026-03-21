namespace NiTorrent.Domain.Torrents;

public static class TorrentDuplicatePolicy
{
    public static bool IsDuplicate(TorrentEntry existing, TorrentKey key, string savePath)
    {
        if (!existing.Key.IsEmpty && !key.IsEmpty && string.Equals(existing.Key.Value, key.Value, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(existing.SavePath, savePath, StringComparison.OrdinalIgnoreCase);
    }
}
