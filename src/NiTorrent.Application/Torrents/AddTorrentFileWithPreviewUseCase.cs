using static NiTorrent.Application.Torrents.TorrentSource;

namespace NiTorrent.Application.Torrents;

public sealed class AddTorrentFileWithPreviewUseCase(
    ITorrentPreviewFlow previewFlow)
{
    public Task<bool> ExecuteAsync(string filePath, CancellationToken ct = default)
        => previewFlow.ExecuteAsync(new TorrentFile(filePath), ct);
}
