using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentWriteService
{
    Task<TorrentRuntimeStateOld> AddAsync(TorrentId id, AddTorrentRequest request, CancellationToken ct = default);
    Task<TorrentRuntimeStateOld> RehydrateAsync(TorrentEntry entry, byte[] torrentBytes, CancellationToken ct = default);
    Task ApplySettingsAsync(CancellationToken ct = default);
}
