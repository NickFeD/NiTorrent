namespace NiTorrent.Application.Torrents.DTo;

public sealed class TorrentRuntimeStateChangedEventArgs(IReadOnlyList<TorrentRuntimeStatus> updatedStatuses, IReadOnlyList<Guid> removedIds) : EventArgs
{
    public IReadOnlyList<TorrentRuntimeStatus> UpdatedStatuses { get; } = updatedStatuses;
    public IReadOnlyList<Guid> RemovedIds { get; } = removedIds;
}
