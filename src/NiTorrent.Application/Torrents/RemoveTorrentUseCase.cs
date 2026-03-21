using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class RemoveTorrentUseCase(ITorrentWriteService writeService)
{
    public Task ExecuteAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => writeService.RemoveAsync(id, deleteData, ct);
}
