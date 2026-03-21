using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class RemoveTorrentUseCase(ITorrentService torrentService)
{
    public Task ExecuteAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => torrentService.RemoveAsync(id, deleteData, ct);
}
