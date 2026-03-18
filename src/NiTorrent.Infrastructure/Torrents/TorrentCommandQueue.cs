using System.Collections.Concurrent;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// In-memory queue of "user intent" commands issued before the torrent engine is ready.
/// We intentionally keep it very small: last command wins per torrent.
/// </summary>
internal sealed class TorrentCommandQueue
{
    private readonly ConcurrentDictionary<TorrentId, bool> _desiredRunning = new();

    public void SetDesiredRunning(TorrentId id, bool shouldRun)
        => _desiredRunning[id] = shouldRun;

    public IReadOnlyDictionary<TorrentId, bool> SnapshotAndClear()
    {
        var snapshot = _desiredRunning.ToArray()
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        _desiredRunning.Clear();
        return snapshot;
    }
}
