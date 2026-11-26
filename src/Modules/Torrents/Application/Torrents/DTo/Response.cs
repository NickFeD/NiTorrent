using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.DTo;

public sealed record StartedTorrentDownloadResponse(TorrentDownload TorrentDownload);

public sealed record StartTorrentResponse(Guid TorrentId, TorrentDownloadStatus Status);

public sealed record PauseTorrentResponse(Guid TorrentId, TorrentDownloadStatus Status);
public sealed record DeleteTorrentResponse(Guid TorrentId);
