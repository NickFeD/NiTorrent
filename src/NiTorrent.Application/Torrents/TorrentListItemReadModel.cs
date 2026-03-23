using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed record TorrentListItemReadModel(
    TorrentId Id,
    string Key,
    string Name,
    long Size,
    string SavePath,
    DateTimeOffset AddedAtUtc,
    TorrentStatus Status
);
