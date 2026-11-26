using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentRuntimeStateSource
{
    bool TryGet(Guid torrentId, out TorrentRuntimeStatus status);
    IReadOnlyDictionary<Guid, TorrentRuntimeStatus> GetSnapshot();
}
