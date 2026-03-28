using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed record PreparedTorrentSource(
    byte[] TorrentBytes,
    TorrentKey Key,
    string Name,
    long TotalSize,
    IReadOnlyList<TorrentFileEntry> Files,
    bool HasMetadata = true);
