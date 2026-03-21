using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentEngineMaintenanceService(ITorrentService torrentService) : ITorrentEngineMaintenanceService
{
    public Task SaveStateAsync(CancellationToken ct = default)
        => torrentService.SaveAsync(ct);

    public Task ShutdownAsync(CancellationToken ct = default)
        => torrentService.ShutdownAsync(ct);
}
