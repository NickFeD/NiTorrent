using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentStartupRecovery
{
    private readonly ILogger<TorrentStartupRecovery> _logger;
    private readonly TorrentCatalogStore _catalogStore;

    public TorrentStartupRecovery(ILogger<TorrentStartupRecovery> logger, TorrentCatalogStore catalogStore)
    {
        _logger = logger;
        _catalogStore = catalogStore;
    }

    public Task<IReadOnlyList<PendingRemoval>> AttachRestoredManagersAsync(
        ClientEngine engine,
        TorrentRuntimeRegistry runtimeRegistry,
        Func<TorrentManager, string> stableKey,
        CancellationToken ct = default)
        => _catalogStore.AttachRestoredManagersAsync(engine, runtimeRegistry.AsDictionary(), stableKey, ct);

    public async Task CompletePendingRemovalsAsync(
        ClientEngine engine,
        IReadOnlyList<PendingRemoval> pendingRemovals,
        CancellationToken ct = default)
    {
        foreach (var pendingRemoval in pendingRemovals)
        {
            try
            {
                var mode = pendingRemoval.DeleteDownloadedData
                    ? RemoveMode.CacheDataAndDownloadedData
                    : RemoveMode.CacheDataOnly;

                await engine.RemoveAsync(pendingRemoval.Manager, mode).ConfigureAwait(false);
                await _catalogStore.CompletePendingRemovalAsync(pendingRemoval.Key, ct).ConfigureAwait(false);
                _logger.LogInformation("Removed pending cached torrent {TorrentKey} after engine restore", pendingRemoval.Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove pending cached torrent {TorrentKey}", pendingRemoval.Key);
            }
        }

        await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
    }

    public async Task ApplyQueuedIntentAsync(
        TorrentCommandQueue commandQueue,
        TorrentRuntimeRegistry runtimeRegistry,
        CancellationToken ct = default)
    {
        var desired = commandQueue.SnapshotAndClear();
        if (desired.Count == 0)
            return;

        foreach (var (id, shouldRun) in desired)
        {
            if (!runtimeRegistry.TryGet(id, out var manager) || manager is null)
                continue;

            try
            {
                if (shouldRun)
                    await manager.StartAsync().ConfigureAwait(false);
                else
                    await manager.PauseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply queued command for torrent {TorrentId}", id.Value);
            }
        }

        await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
    }

    public async Task AutoStartFromCatalogAsync(
        TorrentRuntimeRegistry runtimeRegistry,
        CancellationToken ct = default)
    {
        foreach (var (id, manager) in runtimeRegistry.Snapshot())
        {
            var (found, shouldRun) = await _catalogStore.TryGetShouldRunAsync(id, ct).ConfigureAwait(false);
            if (!found || !shouldRun)
                continue;

            if (manager.State is TorrentState.Downloading or TorrentState.Seeding or TorrentState.Hashing or TorrentState.FetchingHashes or TorrentState.Metadata)
                continue;

            try
            {
                await manager.StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-start torrent {TorrentId}", id.Value);
            }
        }

        await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
    }
}
