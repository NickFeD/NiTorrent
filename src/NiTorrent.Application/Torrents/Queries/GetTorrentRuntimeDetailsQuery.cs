using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Queries;

public sealed class GetTorrentRuntimeDetailsQuery(
    ITorrentDetailsRuntimeService detailsRuntimeService)
{
    public Task<TorrentRuntimeDetailsSnapshot?> ExecuteAsync(TorrentId torrentId, CancellationToken ct = default)
        => detailsRuntimeService.TryGetAsync(torrentId, ct);
}
