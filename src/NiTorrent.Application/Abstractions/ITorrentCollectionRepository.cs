using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentCollectionRepository
{
    Task<IReadOnlyList<TorrentEntry>> GetAllAsync(CancellationToken ct = default);
    Task<TorrentEntry?> TryGetAsync(TorrentId id, CancellationToken ct = default);
    Task<TorrentEntry?> TryGetByKeyAsync(TorrentKey key, CancellationToken ct = default);
    Task UpsertAsync(TorrentEntry entry, CancellationToken ct = default);
    Task RemoveAsync(TorrentId id, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
