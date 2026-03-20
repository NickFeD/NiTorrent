using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentQueryService
{
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly TorrentSnapshotFactory _snapshotFactory;

    public TorrentQueryService(
        TorrentRuntimeRegistry runtimeRegistry,
        TorrentSnapshotFactory snapshotFactory)
    {
        _runtimeRegistry = runtimeRegistry;
        _snapshotFactory = snapshotFactory;
    }

    public IReadOnlyList<TorrentSnapshot> GetAll(bool hasEngine)
    {
        if (!hasEngine)
            return [];

        return [.. _runtimeRegistry.Snapshot().Select(kv => _snapshotFactory.Create(kv.Key, kv.Value, addedAtUtc: null))];
    }

    public TorrentSnapshot? TryGet(TorrentId id)
    {
        if (_runtimeRegistry.TryGet(id, out var manager) && manager is not null)
            return _snapshotFactory.Create(id, manager, addedAtUtc: null);

        return null;
    }
}
