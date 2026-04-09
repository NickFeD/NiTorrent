using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed record TorrentRuntimeDetailsSnapshot(
    TorrentId Id,
    string StableKey,
    TorrentStatus Status,
    long? TotalSizeBytes,
    long DownloadedBytes,
    long UploadedBytes,
    long RemainingBytes,
    TimeSpan? Eta,
    double Ratio,
    int? PieceSizeBytes,
    int? PieceCount,
    int? OpenConnections,
    int? UploadingToConnections,
    int? PeerCount,
    int? SeedCount,
    int? HashFailCount,
    int? UnhashedPieceCount,
    IReadOnlyList<TorrentPeerSnapshot> Peers,
    IReadOnlyList<TorrentTrackerSnapshot> Trackers);

public sealed record TorrentPeerSnapshot(
    string Key,
    string Endpoint,
    string? Client,
    double? ProgressPercent,
    long? DownloadRateBytesPerSecond,
    long? UploadRateBytesPerSecond,
    double? Ratio,
    bool? IsSeeder,
    bool? IsInterested,
    bool? IsChoking);

public sealed record TorrentTrackerSnapshot(
    string Key,
    string Uri,
    string Status,
    TimeSpan? LastAnnounceAgo,
    TimeSpan? NextAnnounceIn,
    string? Warning,
    string? Failure);
