using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Transition-only repository. Exposes the product-owned torrent collection contract,
/// but stores data in the legacy catalog until the dedicated product storage replaces it.
/// </summary>
public sealed class CatalogBackedTorrentCollectionRepository(TorrentCatalogStore catalogStore) : ITorrentCollectionRepository
{
    public Task<IReadOnlyList<TorrentEntry>> GetAllAsync(CancellationToken ct = default)
        => catalogStore.GetEntriesAsync(ct);

    public Task<TorrentEntry?> TryGetAsync(TorrentId id, CancellationToken ct = default)
        => catalogStore.TryGetEntryAsync(id, ct);

    public Task<TorrentEntry?> TryGetByKeyAsync(TorrentKey key, CancellationToken ct = default)
        => catalogStore.TryGetEntryByKeyAsync(key, ct);

    public Task UpsertAsync(TorrentEntry entry, CancellationToken ct = default)
        => catalogStore.UpsertEntryAsync(entry, ct);

    public Task RemoveAsync(TorrentId id, CancellationToken ct = default)
        => catalogStore.RemoveEntryAsync(id, ct);
}
