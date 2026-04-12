using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentRuntimeStateStore
{
    bool TryGet(Guid torrentId, out TorrentRuntimeStatus status);
    IReadOnlyDictionary<Guid, TorrentRuntimeStatus> GetSnapshot();
    void Update(IReadOnlyList<TorrentRuntimeStatus> statuses);

    event EventHandler<TorrentRuntimeStateChangedEventArgs>? Changed;
}
