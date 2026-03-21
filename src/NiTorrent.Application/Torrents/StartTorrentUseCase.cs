using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class StartTorrentUseCase(ITorrentCommandService torrentCommandService)
{
    public async Task ExecuteAsync(TorrentId id, CancellationToken ct = default)
        => _ = await torrentCommandService.StartAsync(id, ct).ConfigureAwait(false);
}
