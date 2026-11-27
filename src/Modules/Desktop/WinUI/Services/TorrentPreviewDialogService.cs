using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Torrents;
using NiTorrent.Application.Torrents;
using NiTorrent.Torrents.Contracts;

namespace NiTorrent.App.Services;

public sealed class TorrentPreviewDialogService(ITorrentsModule torrents) : ITorrentPreviewService
{
    public async Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentSource source,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var window = new TorrentPreviewWindow(new TorrentPreviewViewModel(token => torrents.PreviewAsync(source, token)));
        using var registration = ct.Register(() => window.DispatcherQueue.TryEnqueue(() => window.Close()));
        window.Activate();

        return await window.WaitForResultAsync();
    }
}
