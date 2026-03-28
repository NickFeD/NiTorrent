using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentEngineGateway
{
    Task<bool> StartAsync(TorrentId id, CancellationToken ct = default);
    Task<bool> PauseAsync(TorrentId id, CancellationToken ct = default);
    Task StopAsync(TorrentId id, CancellationToken ct = default);
    Task<bool> RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default);
}
