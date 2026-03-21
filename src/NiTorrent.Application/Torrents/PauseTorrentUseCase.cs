using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class PauseTorrentUseCase(ITorrentWriteService writeService)
{
    public Task ExecuteAsync(TorrentId id, CancellationToken ct = default)
        => writeService.PauseAsync(id, ct);
}
