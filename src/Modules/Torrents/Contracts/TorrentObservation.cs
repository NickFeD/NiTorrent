using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Torrents.Contracts;

public enum TorrentObservationKind { Summary, Collection, Torrent, Peers, Trackers }
public enum TorrentConnectionState { Waiting, Connected, Disconnected, Unsupported }
public sealed record TorrentObservationRequest(TorrentObservationKind Kind, Guid? TorrentId = null);
public sealed record TorrentObservationSnapshot(IReadOnlyDictionary<Guid, TorrentRuntimeStatus> Statuses,
    long DownloadSpeed, long UploadSpeed, TorrentConnectionState Connection, string? Message = null);

public interface ITorrentObservations
{
    /// <summary>Independent lease. Serial delivery keeps only the latest pending state.</summary>
    IDisposable Observe(TorrentObservationRequest request,
        Func<TorrentObservationSnapshot, CancellationToken, Task> observer);
}
