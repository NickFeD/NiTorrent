using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.Infrastructure.Torrents;

public class TorrentEngineCoordinator
{
    private readonly ITorrentRepository _torrentRepository;
    private readonly ConcurrentDictionary<Guid, TorrentManager> _torrentManagers = new();
    public ClientEngine Engine { get; set; }

    public TorrentEngineCoordinator(TorrentEngineFactory torrentEngineFactory, ITorrentRepository torrentRepository)
    {
        _torrentRepository = torrentRepository;
        Engine = torrentEngineFactory.CreateAsync().GetAwaiter().GetResult();
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
    public Dictionary<Guid, TorrentManager> GetTorrentMap()
    {
        return new Dictionary<Guid, TorrentManager>(_torrentManagers);
    }

    internal bool TryGetTorrent(Guid id, out TorrentManager? manager)
    {
       return _torrentManagers.TryGetValue(id, out manager);
    }
}
