using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents.LegacyAdapters;

public sealed class LegacyTorrentEngineMaintenanceService(ITorrentService torrentService) : ITorrentEngineMaintenanceService
{
    public Task SaveAsync(CancellationToken ct = default) => torrentService.SaveAsync(ct);
    public Task ShutdownAsync(CancellationToken ct = default) => torrentService.ShutdownAsync(ct);
}
