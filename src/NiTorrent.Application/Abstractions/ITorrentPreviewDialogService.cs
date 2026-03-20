using NiTorrent.Application.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentPreviewDialogService
{
    Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentPreview preview,
        CancellationToken ct = default);
}

public sealed record TorrentPreviewDialogResult(
    IReadOnlyList<string> SelectedFilePaths,
    string OutputFolder
);
