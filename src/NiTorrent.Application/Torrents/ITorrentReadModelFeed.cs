using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentReadModelFeed
{
    event Action<IReadOnlyList<TorrentSnapshot>>? Updated;
    IReadOnlyList<TorrentSnapshot> Current { get; }
    void Refresh();
}
