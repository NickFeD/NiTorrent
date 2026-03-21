namespace NiTorrent.Application.Abstractions;

public interface ITorrentEngineMaintenanceService
{
    Task SaveAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}
