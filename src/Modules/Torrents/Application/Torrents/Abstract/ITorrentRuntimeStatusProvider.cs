using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentRuntimeStatusProvider
{
    Task<TorrentRuntimeStatus?> GetAsync(Guid torrentId, CancellationToken ct);
    Task<IReadOnlyList<TorrentRuntimeStatus>> GetAllAsync(CancellationToken ct);
}
//NOTE: This interface is intended to be used for querying the current status of torrents, such as their progress, speed, etc. It is not designed for real-time event listening. For real-time updates, a separate event source or notification mechanism should be implemented, such as an IAsyncEnumerable<TorrentRuntimeEvent> that clients can subscribe to for receiving updates as they occur.
//public interface ITorrentRuntimeEventSource
//{
//    IAsyncEnumerable<TorrentRuntimeEvent> ListenAsync(CancellationToken ct);
//}
