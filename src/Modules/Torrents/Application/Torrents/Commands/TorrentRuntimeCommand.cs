namespace NiTorrent.Application.Torrents.Commands;

public sealed record StartTorrentCommand(Guid TorrentId);

public sealed record PauseTorrentCommand(Guid TorrentId);

public sealed record DeleteTorrentCommand(Guid TorrentId, bool DeleteFiles);
