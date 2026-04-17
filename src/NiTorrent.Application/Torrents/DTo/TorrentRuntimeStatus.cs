using NiTorrent.Application.Torrents.Enum;

namespace NiTorrent.Application.Torrents.DTo;

public sealed record TorrentRuntimeStatus(Guid TorrentId, TorrentLifecycleState State, string? ErrorMessage, long DownloadSpeed, double Progress);
