using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentCatalogSnapshotSynchronizer
{
    private readonly TorrentCatalogStore _catalogStore;

    public TorrentCatalogSnapshotSynchronizer(TorrentCatalogStore catalogStore)
    {
        _catalogStore = catalogStore;
    }

    public async Task SyncAsync(IReadOnlyList<TorrentSnapshot> snapshots, CancellationToken ct)
    {
        foreach (var snapshot in snapshots)
        {
            await _catalogStore.UpsertFromSnapshotAsync(snapshot, ct).ConfigureAwait(false);
        }
    }

    public Task SaveAsync(CancellationToken ct)
        => _catalogStore.SaveAsync(force: false, ct);
}
