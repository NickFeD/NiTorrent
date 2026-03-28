using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentWriteService
{
    Task<TorrentRuntimeState> AddAsync(TorrentId id, AddTorrentRequest request, CancellationToken ct = default);
    Task ApplySettingsAsync(CancellationToken ct = default);
}
