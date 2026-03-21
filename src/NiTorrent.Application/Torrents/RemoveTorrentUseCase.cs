using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class RemoveTorrentUseCase(ITorrentCommandService torrentCommandService)
{
    public async Task ExecuteAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => _ = await torrentCommandService.RemoveAsync(id, deleteData, ct).ConfigureAwait(false);
}
