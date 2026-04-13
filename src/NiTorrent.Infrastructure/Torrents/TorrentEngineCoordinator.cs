using System.Collections.Concurrent;
using MonoTorrent.Client;

namespace NiTorrent.Infrastructure.Torrents;

public class TorrentEngineCoordinator
{
    private readonly ConcurrentDictionary<Guid, TorrentManager> _torrentManagers = new();
    private ClientEngine? _engine;

    public bool IsInitialized => _engine is not null;

    public ClientEngine Engine =>
        _engine ?? throw new InvalidOperationException("ClientEngine is not initialized.");

    public Task InitializeAsync(EngineSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_engine is not null)
            throw new InvalidOperationException("ClientEngine already initialized.");
        _engine = new ClientEngine(settings);

        return Task.CompletedTask;
    }

    public async Task ApplySettingsAsync(EngineSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_engine is null)
        {
            _engine = new ClientEngine(settings);
            return;
        }

        await _engine.UpdateSettingsAsync(settings).WaitAsync(ct);
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
