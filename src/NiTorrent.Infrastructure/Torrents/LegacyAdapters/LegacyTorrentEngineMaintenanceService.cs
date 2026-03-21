using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;

namespace NiTorrent.Infrastructure.Torrents.LegacyAdapters;

/// <summary>
/// Transition-only maintenance service over legacy ITorrentService.
/// </summary>
public sealed class LegacyTorrentEngineMaintenanceService(ITorrentService torrentService) : ITorrentEngineMaintenanceService
{
    public Task SaveStateAsync(CancellationToken ct = default)
        => torrentService.SaveAsync(ct);

    public Task ShutdownAsync(CancellationToken ct = default)
        => torrentService.ShutdownAsync(ct);
}
