using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class AddTorrentUseCase(ITorrentWriteService writeService)
{
    public async Task ExecuteAsync(AddTorrentRequest request, CancellationToken ct = default)
        => _ = await writeService.AddAsync(request, ct).ConfigureAwait(false);
}
