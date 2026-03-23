using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentEngineGateway
{
    Task StartAsync(TorrentId id, CancellationToken ct = default);
    Task PauseAsync(TorrentId id, CancellationToken ct = default);
    Task StopAsync(TorrentId id, CancellationToken ct = default);
    Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default);
}
