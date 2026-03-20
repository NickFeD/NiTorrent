using MonoTorrent;
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

        var downloadRate = phase is TorrentPhase.Paused or TorrentPhase.Stopped ? 0 : manager.Monitor.DownloadRate;
        var uploadRate = phase is TorrentPhase.Paused or TorrentPhase.Stopped ? 0 : manager.Monitor.UploadRate;

        var status = new TorrentStatus(
            phase,
            isComplete,
            progress,
            downloadRate,
            uploadRate,
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
        }
        catch
        {
            return string.Empty;
        }
    }

    public string GetStableKey(Torrent? torrent)
        => torrent is null ? string.Empty : GetStableKey(torrent.InfoHashes);

    private static string GetStableKey(InfoHashes? infoHashes)
    {
        var v1 = infoHashes?.V1;
        if (v1 is not null)
            return v1.ToHex();

        var v2 = infoHashes?.V2;
        return v2?.ToHex() ?? string.Empty;
    }
}
