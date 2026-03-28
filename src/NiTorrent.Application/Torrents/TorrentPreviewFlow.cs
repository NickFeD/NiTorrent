using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentPreviewFlow(
    ITorrentSourcePreparationService sourcePreparationService,
    ITorrentPreviewDialogService previewDialog,
    AddTorrentUseCase addTorrentUseCase,
    ITorrentCollectionRepository collectionRepository) : ITorrentPreviewFlow
{
    public async Task<bool> ExecuteAsync(TorrentSource source, CancellationToken ct = default)
    {
        var prepared = await sourcePreparationService.PrepareAsync(source, ct).ConfigureAwait(false);
        await EnsureNoDuplicateByKeyAsync(prepared, ct).ConfigureAwait(false);

        var preview = new TorrentPreview(prepared.Name, prepared.TotalSize, prepared.Files);
        var dialogResult = await previewDialog.ShowAsync(preview, ct).ConfigureAwait(false);
        if (dialogResult is null)
            return false;

        await addTorrentUseCase.ExecuteAsync(new AddTorrentRequest(
            prepared,
            dialogResult.OutputFolder,
            dialogResult.SelectedFilePaths.ToHashSet(StringComparer.OrdinalIgnoreCase)), ct).ConfigureAwait(false);

        return true;
    }

    private async Task EnsureNoDuplicateByKeyAsync(PreparedTorrentSource prepared, CancellationToken ct)
    {
        if (prepared.Key.IsEmpty)
            return;

        var existing = await collectionRepository.GetAllAsync(ct).ConfigureAwait(false);
        if (TorrentDuplicatePolicy.FindDuplicateByKey(existing, prepared.Key) is not null)
            throw new UserVisibleException("Этот торрент уже добавлен в приложение.");
    }
}
