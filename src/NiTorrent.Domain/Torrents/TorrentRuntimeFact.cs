namespace NiTorrent.Domain.Torrents;

public sealed record TorrentRuntimeFact(
    TorrentId Id,
    TorrentKey Key,
    string Name,
    string SavePath,
    TorrentRuntimeState Runtime
);
