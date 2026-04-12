using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentRuntimeStateSource
{
    void Subscribe(Func<TorrentRuntimeStateChangedEventArgs, Task> handler);
    Task UnsubscribeAsync(Func<TorrentRuntimeStateChangedEventArgs, Task> handler);
    bool TryGet(Guid torrentId, out TorrentRuntimeStatus status);
    IReadOnlyDictionary<Guid, TorrentRuntimeStatus> GetSnapshot();
    void Update(IReadOnlyList<TorrentRuntimeStatus> statuses);
}
