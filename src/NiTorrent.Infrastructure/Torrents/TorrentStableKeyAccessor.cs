using MonoTorrent;
using MonoTorrent.Client;

namespace NiTorrent.Infrastructure.Torrents;

internal static class TorrentStableKeyAccessor
{
    public static string GetStableKey(Torrent torrent)
    {
        try
        {
            var infoHashes = torrent.InfoHashes;
            var v1 = infoHashes?.V1;
            if (v1 is not null)
                return v1.ToString() ?? string.Empty;

            var v2 = infoHashes?.V2;
            return v2?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetStableKey(TorrentManager manager)
    {
        try
        {
            var infoHashes = manager.InfoHashes;
            var v1 = infoHashes?.V1;
            if (v1 is not null)
                return v1.ToString() ?? string.Empty;

            var v2 = infoHashes?.V2;
            return v2?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
