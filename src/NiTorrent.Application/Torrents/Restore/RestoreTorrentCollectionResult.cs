using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed record RestoreTorrentCollectionResult(
    IReadOnlyList<TorrentEntry> EarlyCollection,
    IReadOnlyList<TorrentEntry> RestoredCollection,
    IReadOnlyList<TorrentRuntimeFact> RuntimeFacts
);
