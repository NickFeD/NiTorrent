using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Abstractions;

public interface ITorrentPreviewService
{
    Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentSource source,
        CancellationToken ct = default);
}

public sealed record TorrentPreviewDialogResult(
    IReadOnlyList<TorrentFileEntry> SelectedFiles,
    string OutputFolder
);
