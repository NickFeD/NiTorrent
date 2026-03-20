using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentPreviewFlow(
    ITorrentService torrentService,
    ITorrentPreviewDialogService previewDialog,
    AddTorrentUseCase addTorrentUseCase) : ITorrentPreviewFlow
{
    public async Task<bool> ExecuteAsync(TorrentSource source, CancellationToken ct = default)
    {
        var preview = await torrentService.GetPreviewAsync(source, ct).ConfigureAwait(false);
        var dialogResult = await previewDialog.ShowAsync(preview, ct).ConfigureAwait(false);
        if (dialogResult is null)
            return false;

        await addTorrentUseCase.ExecuteAsync(new AddTorrentRequest(
            source,
            dialogResult.OutputFolder,
            dialogResult.SelectedFilePaths.ToHashSet()), ct).ConfigureAwait(false);

        return true;
    }
}
