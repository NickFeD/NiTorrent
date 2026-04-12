using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class InMemoryTorrentRuntimeStateStore : ITorrentRuntimeStateStore
{
    private readonly Dictionary<Guid, TorrentRuntimeStatus> _statuses = new();

    public event EventHandler<TorrentRuntimeStateChangedEventArgs>? Changed;

    public bool TryGet(Guid torrentId, out TorrentRuntimeStatus status)
        => _statuses.TryGetValue(torrentId, out status!);

    public IReadOnlyDictionary<Guid, TorrentRuntimeStatus> GetSnapshot()
        => new Dictionary<Guid, TorrentRuntimeStatus>(_statuses);

    public void Update(IReadOnlyList<TorrentRuntimeStatus> statuses)
    {
        var incomingById = statuses.ToDictionary(x => x.TorrentId);
        var changed = new List<TorrentRuntimeStatus>();

        foreach (var status in statuses)
        {
            if (_statuses.TryGetValue(status.TorrentId, out var existing) && existing == status)
                continue;

            _statuses[status.TorrentId] = status;
            changed.Add(status);
        }

        var removedIds = _statuses.Keys
            .Where(id => !incomingById.ContainsKey(id))
            .ToList();

        foreach (var removedId in removedIds)
        {
            _statuses.Remove(removedId);
        }

        if ((changed is { Count: > 0 }) || removedIds.Count > 0)
        {
            Changed?.Invoke(this,
                new TorrentRuntimeStateChangedEventArgs(changed, removedIds));
        }
    }
}
