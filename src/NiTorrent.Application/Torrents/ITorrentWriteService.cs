using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentWriteService
{
    Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default);
    Task AddAsync(AddTorrentRequest request, CancellationToken ct = default);
    Task StartAsync(TorrentId id, CancellationToken ct = default);
    Task PauseAsync(TorrentId id, CancellationToken ct = default);
    Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default);
    Task ApplySettingsAsync(CancellationToken ct = default);
}
