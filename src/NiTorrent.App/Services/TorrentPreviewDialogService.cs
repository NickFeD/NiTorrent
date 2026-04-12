using NiTorrent.Application.Abstractions;
using NiTorrent.Presentation.Features.Torrents;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.App.Services;

public sealed class TorrentPreviewDialogService(IServiceProvider services) : ITorrentPreviewService
{
    private readonly IServiceProvider _services = services;

    public Task<TorrentPreviewDialogResult?> ShowAsync(
        TorrentPreview preview,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var window = new TorrentPreviewWindow(new TorrentPreviewViewModel(preview, _services.GetRequiredService<ITorrentPreferences>()));
        window.Activate();

        return window.WaitForResultAsync();
    }
}
