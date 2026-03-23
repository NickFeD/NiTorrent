using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Deferred;

public sealed record ApplyDeferredTorrentActionsResult(
    IReadOnlyList<TorrentEntry> UpdatedEntries,
    IReadOnlyList<TorrentId> RemovedIds,
    IReadOnlyList<TorrentId> AppliedIds,
    IReadOnlyList<TorrentId> DeferredIds);
