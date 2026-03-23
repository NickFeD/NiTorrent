using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentWriteService
{
    Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default);
    Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default);
    Task ApplySettingsAsync(CancellationToken ct = default);
}
