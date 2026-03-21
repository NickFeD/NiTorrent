using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents.LegacyAdapters;

public sealed class LegacyTorrentEngineMaintenanceService : ITorrentEngineMaintenanceService
{
    private readonly ITorrentService _torrentService;

    public LegacyTorrentEngineMaintenanceService(ITorrentService torrentService)
    {
        _torrentService = torrentService;
    }

    public Task SaveAsync(CancellationToken ct = default)
        => _torrentService.SaveAsync(ct);

    public Task ShutdownAsync(CancellationToken ct = default)
        => _torrentService.ShutdownAsync(ct);
}
