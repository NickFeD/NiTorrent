using System.Collections;
using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Enum;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class EngineBackedTorrentDetailsRuntimeService(
    TorrentRuntimeRegistry runtimeRegistry,
    TorrentStartupCoordinator startupCoordinator,
    TorrentRuntimeContext runtimeContext,
    TorrentStableKeyAccessor stableKeyAccessor) : ITorrentDetailsRuntimeService
{
    private const int MaxPeersInSnapshot = 400;

    public async Task<TorrentRuntimeDetailsSnapshot?> TryGetAsync(TorrentId torrentId, CancellationToken ct = default)
    {
        if (!startupCoordinator.IsReady)
            return null;

        await runtimeContext.OperationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!runtimeRegistry.TryGet(torrentId, out var manager) || manager is null)
                return null;

            return MapManager(torrentId, manager);
        }
        finally
        {
            runtimeContext.OperationGate.Release();
        }
    }

    private TorrentRuntimeDetailsSnapshot MapManager(TorrentId id, TorrentManager manager)
    {
        var phase = manager.State switch
        {
            TorrentState.Metadata => TorrentLifecycleState.FetchingMetadata,
            TorrentState.Hashing or TorrentState.FetchingHashes => TorrentLifecycleState.Checking,
            TorrentState.Downloading => TorrentLifecycleState.Downloading,
            TorrentState.Seeding => TorrentLifecycleState.Seeding,
            TorrentState.Paused => TorrentLifecycleState.Paused,
            TorrentState.Stopped => TorrentLifecycleState.Stopped,
            TorrentState.Error => TorrentLifecycleState.Error,
            _ => TorrentLifecycleState.Unknown
        };

        var progress = manager.PartialProgress;
        var downloadRate = manager.Monitor.DownloadRate;
        var uploadRate = manager.Monitor.UploadRate;
        var status = new TorrentStatus(
            Phase: new TorrentLifecycleStateOld(),
            IsComplete: manager.Complete || progress >= 100.0,
            Progress: progress,
            DownloadRateBytesPerSecond: downloadRate,
            UploadRateBytesPerSecond: uploadRate,
            Error: manager.Error?.ToString());

        long? totalSize = manager.Torrent?.Size;
        var downloadedBytes = Math.Max(0, manager.Monitor.DataBytesReceived);
        var uploadedBytes = Math.Max(0, manager.Monitor.DataBytesSent);
        var remainingBytes = CalculateRemainingBytes(totalSize, downloadedBytes, progress);
        TimeSpan? eta = downloadRate > 0 && remainingBytes > 0
            ? TimeSpan.FromSeconds((double)remainingBytes / downloadRate)
            : null;
        var ratio = downloadedBytes > 0
            ? (double)uploadedBytes / downloadedBytes
            : 0d;

        int? pieceSize = null;
        int? pieceCount = null;
        if (manager.Torrent is not null)
        {
            pieceSize = manager.Torrent.PieceLength;
            pieceCount = manager.Torrent.PieceCount();
        }

        var allPeers = GetManagerPeers(manager);
        var peerCount = allPeers.Count;
        var seedCount = allPeers.Count(x => TryReadBool(x, "IsSeeder") == true);
        var peers = allPeers
            .Take(MaxPeersInSnapshot)
            .Select(MapPeer)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.DownloadRateBytesPerSecond ?? 0)
            .ThenBy(x => x.Endpoint, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var trackers = MapTrackers(manager).ToList();

        var stableKey = stableKeyAccessor.GetStableKey(manager);

        return new TorrentRuntimeDetailsSnapshot(
            Id: id,
            StableKey: stableKey,
            Status: status,
            TotalSizeBytes: totalSize,
            DownloadedBytes: downloadedBytes,
            UploadedBytes: uploadedBytes,
            RemainingBytes: remainingBytes,
            Eta: eta,
            Ratio: ratio,
            PieceSizeBytes: pieceSize,
            PieceCount: pieceCount,
            OpenConnections: manager.OpenConnections,
            UploadingToConnections: manager.UploadingTo,
            PeerCount: peerCount,
            SeedCount: seedCount,
            HashFailCount: TryReadInt(manager, "HashFails"),
            UnhashedPieceCount: TryReadInt(manager, "UnhashedPieces"),
            Peers: peers,
            Trackers: trackers);
    }

    private static long CalculateRemainingBytes(long? totalSize, long downloadedBytes, double progressPercent)
    {
        if (totalSize is > 0)
            return Math.Max(0, totalSize.Value - downloadedBytes);

        if (progressPercent >= 100.0 || progressPercent <= 0.0)
            return 0;

        var estimatedTotal = (long)(downloadedBytes / (progressPercent / 100d));
        return Math.Max(0, estimatedTotal - downloadedBytes);
    }

    private static IEnumerable<TorrentTrackerSnapshot> MapTrackers(TorrentManager manager)
    {
        var trackerManager = manager.TrackerManager;
        if (trackerManager is null)
            return [];

        var tiers = trackerManager.Tiers;
        if (tiers is null)
            return [];

        var result = new List<TorrentTrackerSnapshot>();
        foreach (var tier in tiers)
        {
            var trackersProperty = tier.GetType().GetProperty("Trackers");
            if (trackersProperty?.GetValue(tier) is not IEnumerable trackers)
                continue;

            foreach (var tracker in trackers)
            {
                if (tracker is null)
                    continue;

                var uriValue = TryReadValue<Uri>(tracker, "Uri");
                var uri = uriValue?.ToString() ?? "unknown://tracker";
                var status = TryReadValue<object>(tracker, "Status")?.ToString() ?? "Unknown";
                var timeSinceLastAnnounce = TryReadTimeSpan(tracker, "TimeSinceLastAnnounce");
                var updateInterval = TryReadTimeSpan(tracker, "UpdateInterval");
                TimeSpan? nextAnnounce = null;
                if (timeSinceLastAnnounce.HasValue && updateInterval.HasValue)
                    nextAnnounce = MaxZero(updateInterval.Value - timeSinceLastAnnounce.Value);

                result.Add(new TorrentTrackerSnapshot(
                    Key: uri,
                    Uri: uri,
                    Status: status,
                    LastAnnounceAgo: timeSinceLastAnnounce,
                    NextAnnounceIn: nextAnnounce,
                    Warning: TryReadValue<string>(tracker, "WarningMessage"),
                    Failure: TryReadValue<string>(tracker, "FailureMessage")));
            }
        }

        return result;
    }

    private static TorrentPeerSnapshot? MapPeer(object peer)
    {
        var endpoint = TryGetEndpointKey(peer) ?? "<unknown>";
        var key = endpoint;

        var monitor = TryReadValue<object>(peer, "Monitor");
        var downloadRate = TryReadLong(peer, "DownloadRate")
                           ?? TryReadLong(monitor, "DownloadRate");
        var uploadRate = TryReadLong(peer, "UploadRate")
                         ?? TryReadLong(monitor, "UploadRate");

        return new TorrentPeerSnapshot(
            Key: key,
            Endpoint: endpoint,
            Client: TryReadValue<string>(peer, "ClientApp")
                    ?? TryReadValue<string>(peer, "Software")
                    ?? TryReadValue<string>(TryReadValue<object>(peer, "Peer"), "ClientApp"),
            ProgressPercent: TryReadDouble(peer, "Progress")
                             ?? TryReadDouble(TryReadValue<object>(peer, "Peer"), "Progress"),
            DownloadRateBytesPerSecond: downloadRate,
            UploadRateBytesPerSecond: uploadRate,
            Ratio: TryReadDouble(peer, "Ratio"),
            IsSeeder: TryReadBool(peer, "IsSeeder")
                      ?? TryReadBool(TryReadValue<object>(peer, "Peer"), "IsSeeder"),
            IsInterested: TryReadBool(peer, "AmInterested")
                          ?? TryReadBool(peer, "IsInterested"),
            IsChoking: TryReadBool(peer, "AmChoking")
                       ?? TryReadBool(peer, "IsChoking"));
    }

    private static List<object> GetManagerPeers(TorrentManager manager)
    {
        try
        {
            if (manager.GetType().GetProperty("Peers")?.GetValue(manager) is IEnumerable enumerable)
            {
                var peers = new List<object>();
                foreach (var item in enumerable)
                {
                    if (item is not null)
                        peers.Add(item);
                }

                return peers;
            }
        }
        catch
        {
            // Best effort only.
        }

        return [];
    }

    private static string? TryGetEndpointKey(object? peerLike)
    {
        if (peerLike is null)
            return null;

        if (TryReadValue<Uri>(peerLike, "ConnectionUri") is Uri connectionUri)
            return $"{connectionUri.Host}:{connectionUri.Port}";

        if (TryReadValue<Uri>(peerLike, "Uri") is Uri uri)
            return $"{uri.Host}:{uri.Port}";

        var endpoint = TryReadValue<object>(peerLike, "EndPoint")?.ToString();
        if (!string.IsNullOrWhiteSpace(endpoint))
            return endpoint;

        var connectionEndpoint = TryReadValue<object>(peerLike, "ConnectionEndPoint")?.ToString();
        if (!string.IsNullOrWhiteSpace(connectionEndpoint))
            return connectionEndpoint;

        return null;
    }

    private static TimeSpan MaxZero(TimeSpan value)
        => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    private static T? TryReadValue<T>(object? source, string propertyName)
    {
        if (source is null)
            return default;

        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return default;

        var value = property.GetValue(source);
        if (value is T typed)
            return typed;

        return default;
    }

    private static long? TryReadLong(object? source, string propertyName)
    {
        if (source is null)
            return null;

        var value = TryReadValue<object>(source, propertyName);
        if (value is null)
            return null;

        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            ulong ulongValue when ulongValue <= long.MaxValue => (long)ulongValue,
            _ => null
        };
    }

    private static double? TryReadDouble(object? source, string propertyName)
    {
        if (source is null)
            return null;

        var value = TryReadValue<object>(source, propertyName);
        if (value is null)
            return null;

        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            decimal decimalValue => (double)decimalValue,
            _ => null
        };
    }

    private static bool? TryReadBool(object? source, string propertyName)
    {
        if (source is null)
            return null;

        var value = TryReadValue<object>(source, propertyName);
        return value is bool boolValue ? boolValue : null;
    }

    private static int? TryReadInt(object? source, string propertyName)
    {
        if (source is null)
            return null;

        var value = TryReadValue<object>(source, propertyName);
        return value switch
        {
            int intValue => intValue,
            long longValue when longValue is <= int.MaxValue and >= int.MinValue => (int)longValue,
            _ => null
        };
    }

    private static TimeSpan? TryReadTimeSpan(object? source, string propertyName)
    {
        if (source is null)
            return null;

        var value = TryReadValue<object>(source, propertyName);
        return value is TimeSpan ts ? ts : null;
    }
}
