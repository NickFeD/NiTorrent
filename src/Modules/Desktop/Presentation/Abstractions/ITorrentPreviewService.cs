using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Abstractions;

public interface ITorrentPreviewService
{
    Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentPreview preview,
        CancellationToken ct = default);
}

public sealed record TorrentPreviewDialogResult(
    IReadOnlyList<TorrentFileEntry> SelectedFiles,
    string OutputFolder
);
