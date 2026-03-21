using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentEngineGateway
{
    Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default);
    Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default);
    Task StartAsync(TorrentId id, CancellationToken ct = default);
    Task PauseAsync(TorrentId id, CancellationToken ct = default);
    Task StopAsync(TorrentId id, CancellationToken ct = default);
    Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default);
    void PublishUpdates();
}
