using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;

namespace NiTorrent.Infrastructure.Torrents;

public class TorrentEngineCoordinator
{
    private readonly ConcurrentDictionary<Guid, TorrentManager> _torrentManagers = new();
    public ClientEngine Engine { get; set; }

    public TorrentEngineCoordinator(ClientEngine engine)
    {
        Engine = engine;
    }

    public void AddTorrent(Guid id, TorrentManager manager)
    {
        var isAdd = _torrentManagers.TryAdd(id, manager);
        if (!isAdd)
            throw new InvalidOperationException($"Не удалось добавить торрент '{manager.Name}' с id '{id}'.");
    }

    public void RemoveTorrent(Guid id)
    {
        _torrentManagers.TryRemove(id, out _);
    }

    public TorrentManager GetTorrent(Guid id)
    {
        return _torrentManagers[id];
    }
}
