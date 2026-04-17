using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.App.Services;

public sealed class TorrentPreviewDialogService(IServiceProvider services) : ITorrentPreviewService
{
    private readonly IServiceProvider _services = services;

    public Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentPreview preview,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var window = new TorrentPreviewWindow(new TorrentPreviewViewModel(preview));
        window.Activate();

        return window.WaitForResultAsync();
    }
}
