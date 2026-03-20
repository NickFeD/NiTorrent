using NiTorrent.Application.Abstractions;
using static NiTorrent.Application.Torrents.TorrentSource;

namespace NiTorrent.Application.Torrents;

public sealed class PickAndAddTorrentUseCase(
    IPickerHelper pickerHelper,
    ITorrentPreviewFlow previewFlow)
{
    public async Task<bool> ExecuteAsync(CancellationToken ct = default)
    {
        var path = await pickerHelper.PickSingleFilePathAsync(".torrent");
        if (path is null)
            return false;

        return await previewFlow.ExecuteAsync(new TorrentFile(path), ct).ConfigureAwait(false);
    }
}
