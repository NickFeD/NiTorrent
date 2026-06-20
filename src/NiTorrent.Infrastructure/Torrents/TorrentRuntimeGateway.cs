using MonoTorrent;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

internal class TorrentRuntimeGateway(
    TorrentEngineCoordinator coordinator,
    TorrentRuntimeWorkGate workGate) : ITorrentRuntimeGateway
{
    private static readonly TimeSpan TorrentStopTimeout = TimeSpan.FromSeconds(3);
    private readonly TorrentEngineCoordinator _coordinator = coordinator;
    private readonly TorrentRuntimeWorkGate _workGate = workGate;


    public async Task AddAsync(Guid id, TorrentSource source, string savePath, CancellationToken ct)
    {
        _workGate.ThrowIfNotAcceptingWork();
        ct.ThrowIfCancellationRequested();

        var torrentManager = await (source switch
        {
            TorrentSource.TorrentFile tf => _coordinator.Engine.AddAsync(tf.Path, savePath),
            TorrentSource.Magnet m => _coordinator.Engine.AddAsync(MagnetLink.Parse(m.Uri), savePath),
            TorrentSource.TorrentBytes b => _coordinator.Engine.AddAsync(Torrent.Load(b.Bytes), savePath),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        });

        _coordinator.AddTorrent(id, torrentManager);
    }

    public async Task UpdateFileSelectionAsync(Guid id, List<TorrentFileEntry> torrentFiles, CancellationToken ct)
    {
        _workGate.ThrowIfNotAcceptingWork();
        var manager = _coordinator.GetTorrent(id);

        var selectedFiles = torrentFiles.ToDictionary(x => x.FullPath, x => x.IsSelected);

        foreach (var managerFile in manager.Files)
        {
            ct.ThrowIfCancellationRequested();

            if (!selectedFiles.TryGetValue(managerFile.Path, out var isSelected))
                continue;

            var targetPriority = isSelected ? Priority.Normal : Priority.DoNotDownload;

            if (managerFile.Priority == targetPriority)
                continue;

            await manager.SetFilePriorityAsync(managerFile, targetPriority);
        }
    }

    public Task StartAsync(Guid id, CancellationToken ct)
    {
        _workGate.ThrowIfNotAcceptingWork();
        ct.ThrowIfCancellationRequested();
        return _coordinator.GetTorrent(id).StartAsync();
    }

    public Task PauseAsync(Guid torrentId, CancellationToken ct)
    {
        _workGate.ThrowIfNotAcceptingWork();
        ct.ThrowIfCancellationRequested();
        return _coordinator.GetTorrent(torrentId).PauseAsync();
    }

    public async Task RemoveAsync(Guid torrentId, bool deleteFiles, CancellationToken ct)
    {
        _workGate.ThrowIfNotAcceptingWork();
        ct.ThrowIfCancellationRequested();
        var manager = _coordinator.GetTorrent(torrentId);
        _coordinator.RemoveTorrent(torrentId);
        await manager.StopAsync(TorrentStopTimeout);
        await _coordinator.Engine.RemoveAsync(manager);
    }

    public Task<bool> ExistsByIdAsync(Guid id)
    {
        return Task.FromResult(_coordinator.TryGetTorrent(id, out _));
    }
}
