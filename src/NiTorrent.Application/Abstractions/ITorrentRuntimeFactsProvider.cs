using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentRuntimeFactsProvider
{
    IReadOnlyList<TorrentRuntimeFact> GetAll();
    event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;
}
