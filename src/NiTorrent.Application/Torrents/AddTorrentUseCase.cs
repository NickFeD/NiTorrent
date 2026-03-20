using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class AddTorrentUseCase(ITorrentService torrentService)
{
    public Task ExecuteAsync(AddTorrentRequest request, CancellationToken ct = default)
        => torrentService.AddAsync(request, ct);
}
