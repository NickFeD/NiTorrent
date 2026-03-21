namespace NiTorrent.Domain.Torrents;

public sealed record TorrentEntry(
    TorrentId Id,
    TorrentKey Key,
    string Name,
    long Size,
    string SavePath,
    DateTimeOffset AddedAtUtc,
    TorrentIntent Intent,
    TorrentRuntimeState Runtime,
    DeferredAction? DeferredAction = null
);
