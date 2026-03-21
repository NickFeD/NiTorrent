using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentRuntimeFactsProvider
{
    event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;

    IReadOnlyList<TorrentRuntimeFact> GetAll();
    TorrentRuntimeFact? TryGet(TorrentId id);
}
