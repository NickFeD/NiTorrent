using MonoTorrent.Client;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentSnapshotFactory
{
    public TorrentSnapshot Create(TorrentId id, TorrentManager manager, DateTimeOffset? addedAtUtc)
    {
        var phase = manager.State switch
        {
            TorrentState.Metadata => TorrentPhase.FetchingMetadata,
            TorrentState.Hashing or TorrentState.FetchingHashes => TorrentPhase.Checking,
            TorrentState.Downloading => TorrentPhase.Downloading,
            TorrentState.Seeding => TorrentPhase.Seeding,
            TorrentState.Paused => TorrentPhase.Paused,
            TorrentState.Stopped => TorrentPhase.Stopped,
            TorrentState.Error => TorrentPhase.Error,
            _ => TorrentPhase.Unknown
        };

        var progress = manager.PartialProgress;
        var isComplete = progress >= 100.0;

        var status = new TorrentStatus(
            phase,
            isComplete,
            progress,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            manager.Error?.ToString(),
            TorrentSnapshotSource.Live);

        return new TorrentSnapshot(
            id,
            Key: GetStableKey(manager),
            Name: manager.Name,
            Size: manager.Torrent?.Size ?? 0,
            SavePath: manager.SavePath,
            AddedAtUtc: addedAtUtc ?? DateTimeOffset.UtcNow,
            Status: status);
    }

    public string GetStableKey(TorrentManager manager)
    {
        try
        {
            var infoHashes = manager.InfoHashes;
            var v1 = infoHashes?.V1;
            if (v1 is not null)
                return v1.ToString() ?? string.Empty;

            var v2 = infoHashes?.V2;
            return v2?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
