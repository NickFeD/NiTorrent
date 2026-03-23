namespace NiTorrent.Application.Torrents;

public interface ITorrentReadModelFeed
{
    event Action<IReadOnlyList<TorrentListItemReadModel>>? Updated;
    IReadOnlyList<TorrentListItemReadModel> Current { get; }
    void Refresh();
}
