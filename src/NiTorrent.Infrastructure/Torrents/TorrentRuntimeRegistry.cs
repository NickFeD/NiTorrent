using MonoTorrent.Client;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentRuntimeRegistry
{
    private readonly Dictionary<TorrentId, TorrentManager> _byId = new();

    public void Clear() => _byId.Clear();

    public IReadOnlyList<KeyValuePair<TorrentId, TorrentManager>> Snapshot()
        => _byId.ToList();

    public void Set(TorrentId id, TorrentManager manager)
        => _byId[id] = manager;

    public bool TryGet(TorrentId id, out TorrentManager? manager)
        => _byId.TryGetValue(id, out manager);

    public bool Remove(TorrentId id)
        => _byId.Remove(id);

    public bool TryFindIdByStableKey(string stableKey, Func<TorrentManager, string> getStableKey, out TorrentId id)
    {
        foreach (var pair in _byId)
        {
            if (string.Equals(getStableKey(pair.Value), stableKey, StringComparison.OrdinalIgnoreCase))
            {
                id = pair.Key;
                return true;
            }
        }

        id = default;
        return false;
    }

    public Dictionary<TorrentId, TorrentManager> AsDictionary()
        => _byId;
}
