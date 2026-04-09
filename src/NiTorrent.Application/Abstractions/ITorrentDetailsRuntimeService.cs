using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentDetailsRuntimeService
{
    Task<TorrentRuntimeDetailsSnapshot?> TryGetAsync(TorrentId torrentId, CancellationToken ct = default);
}
