namespace NiTorrent.Domain.Torrents;

public sealed record TorrentRuntimeFact(
    TorrentId? Id,
    TorrentKey Key,
    string Name,
    long Size,
    string SavePath,
    TorrentRuntimeState Runtime
);
