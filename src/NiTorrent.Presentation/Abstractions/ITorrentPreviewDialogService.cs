using NiTorrent.Application.Torrents;

namespace NiTorrent.Presentation.Abstractions;

public interface ITorrentPreviewDialogService
{
    /// <summary>
    /// Показывает превью торрента и возвращает выбранные пользователем опции.
    /// null = пользователь отменил.
    /// </summary>
    Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentPreview preview,
        CancellationToken ct = default);
}

public sealed record TorrentPreviewDialogResult(
    IReadOnlyList<string> SelectedFilePaths,
    string OutputFolder
);
