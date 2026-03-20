using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class ApplyTorrentSettingsUseCase(ITorrentService torrentService)
{
    public Task ExecuteAsync(CancellationToken ct = default)
        => torrentService.ApplySettingsAsync();
}
