using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class AddTorrentUseCase(ITorrentWriteService writeService)
{
    public Task ExecuteAsync(AddTorrentRequest request, CancellationToken ct = default)
        => writeService.AddAsync(request, ct);
}
