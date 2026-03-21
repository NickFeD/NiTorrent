using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentReadModelFeed
{
    event Action<IReadOnlyList<TorrentSnapshot>>? Updated;
    IReadOnlyList<TorrentSnapshot> GetAll();
}
