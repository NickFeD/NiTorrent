using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentNotifier
{
    private readonly IDialogService _dialogs;

    public TorrentNotifier(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    public Task NotifyAsync(string title, string message)
        => _dialogs.ShowTextAsync(title, message);
}
