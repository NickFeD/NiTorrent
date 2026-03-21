namespace NiTorrent.Application.Torrents;

public interface ITorrentEngineMaintenanceService
{
    Task SaveStateAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}
