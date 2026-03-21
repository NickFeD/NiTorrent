using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Transition-only repository backed by the legacy torrent catalog.
/// Must be removed once product collection storage becomes first-class.
/// </summary>
public sealed class CatalogBackedTorrentCollectionRepository : ITorrentCollectionRepository
{
    private readonly TorrentCatalogStore _catalogStore;

    public CatalogBackedTorrentCollectionRepository(TorrentCatalogStore catalogStore)
    {
        _catalogStore = catalogStore;
    }

    public Task<IReadOnlyList<TorrentEntry>> GetAllAsync(CancellationToken ct = default) =>
        _catalogStore.GetEntriesAsync(ct);

    public Task<TorrentEntry?> TryGetAsync(TorrentId id, CancellationToken ct = default) =>
        _catalogStore.TryGetEntryAsync(id, ct);

    public Task<TorrentEntry?> TryGetByKeyAsync(TorrentKey key, CancellationToken ct = default) =>
        _catalogStore.TryGetEntryByKeyAsync(key, ct);

    public Task UpsertAsync(TorrentEntry entry, CancellationToken ct = default) =>
        _catalogStore.UpsertEntryAsync(entry, ct);

    public Task RemoveAsync(TorrentId id, CancellationToken ct = default) =>
        _catalogStore.RemoveEntryAsync(id, ct);

    public Task SaveAsync(CancellationToken ct = default) =>
        _catalogStore.SaveAsync(force: true, ct);
}
