using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class ApplyTorrentSettingsUseCase(ITorrentWriteService writeService)
{
    public Task ExecuteAsync(CancellationToken ct = default)
        => writeService.ApplySettingsAsync(ct);
}
