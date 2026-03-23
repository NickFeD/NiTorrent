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
}
