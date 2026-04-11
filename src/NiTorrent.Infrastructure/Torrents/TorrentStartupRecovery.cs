using Microsoft.Extensions.Logging;
using MonoTorrent.Client;

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

    public async Task<IReadOnlyList<PendingRemoval>> AttachRestoredManagersAsync(
        ClientEngine engine,
        TorrentRuntimeRegistry runtimeRegistry,
        Func<TorrentManager, string> stableKey,
        CancellationToken ct = default)
    {
        var result = await _catalogStore.AttachRestoredManagersAsync(engine, stableKey, ct).ConfigureAwait(false);

        runtimeRegistry.Clear();
        foreach (var matched in result.MatchedManagers)
            runtimeRegistry.Set(matched.Id, matched.Manager);

        foreach (var unmatched in result.UnmatchedManagers)
        {
            _logger.LogInformation(
                "Ignoring restored runtime torrent without matching user entry. Key={TorrentKey}; Name={TorrentName}; SavePath={SavePath}",
                unmatched.Key,
                unmatched.Name,
                unmatched.SavePath);
        }

        return result.PendingRemovals;
    }

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

        await _catalogStore.SaveAsync(ct).ConfigureAwait(false);
    }
}
