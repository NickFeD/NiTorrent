using static NiTorrent.Application.Torrents.TorrentSource;

namespace NiTorrent.Application.Torrents;

public sealed class AddMagnetUseCase(ITorrentPreviewFlow previewFlow)
{
    public async Task<bool> ExecuteAsync(string magnet, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(magnet))
            return false;

        return await previewFlow.ExecuteAsync(new Magnet(magnet), ct).ConfigureAwait(false);
    }
}
