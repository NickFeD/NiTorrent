using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class PauseTorrentUseCase(ITorrentService torrentService)
{
    public Task ExecuteAsync(TorrentId id, CancellationToken ct = default)
        => torrentService.PauseAsync(id, ct);
}
