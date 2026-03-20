using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class StartTorrentUseCase(ITorrentService torrentService)
{
    public Task ExecuteAsync(TorrentId id, CancellationToken ct = default)
        => torrentService.StartAsync(id, ct);
}
