using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class RuntimeBackedTorrentRuntimeFactsProvider : ITorrentRuntimeFactsProvider
{
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly TorrentStartupCoordinator _startupCoordinator;
    private readonly SemaphoreSlim _operationGate;

    public event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;

    public RuntimeBackedTorrentRuntimeFactsProvider(
        TorrentRuntimeRegistry runtimeRegistry,
        TorrentEventOrchestrator eventOrchestrator,
        TorrentStartupCoordinator startupCoordinator,
        TorrentRuntimeContext runtimeContext)
    {
        _runtimeRegistry = runtimeRegistry;
        _startupCoordinator = startupCoordinator;
        _operationGate = runtimeContext.OperationGate;

        eventOrchestrator.Invalidated += OnRuntimeChanged;
        eventOrchestrator.Loaded += OnLoaded;
    }

    public IReadOnlyList<TorrentRuntimeFact> GetAll()
    {
        if (!_startupCoordinator.IsReady)
            return Array.Empty<TorrentRuntimeFact>();

        List<KeyValuePair<TorrentId, TorrentManager>> managers;
        _operationGate.Wait();
        try
        {
            managers = _runtimeRegistry.Snapshot().ToList();
        }
        finally
        {
            _operationGate.Release();
        }

        return managers
            .Select(x => MapManager(x.Key, x.Value))
            .ToList();
    }

    private void OnRuntimeChanged()
        => RuntimeFactsUpdated?.Invoke(GetAll());

    private void OnLoaded()
        => RuntimeFactsUpdated?.Invoke(GetAll());

    private static TorrentRuntimeFact MapManager(TorrentId id, TorrentManager manager)
    {
        var state = manager.State switch
        {
            TorrentState.Metadata => TorrentLifecycleState.FetchingMetadata,
            TorrentState.Hashing or TorrentState.FetchingHashes => TorrentLifecycleState.Checking,
            TorrentState.Downloading => TorrentLifecycleState.Downloading,
            TorrentState.Seeding => TorrentLifecycleState.Seeding,
            TorrentState.Paused => TorrentLifecycleState.Paused,
            TorrentState.Stopped => TorrentLifecycleState.Stopped,
            TorrentState.Error => TorrentLifecycleState.Error,
            _ => TorrentLifecycleState.Unknown
        };

        var runtime = new TorrentRuntimeState(
            state,
            manager.PartialProgress >= 100.0,
            manager.PartialProgress,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            manager.Error?.ToString(),
            true);

        var key = string.Empty;
        try
        {
            var hashes = manager.InfoHashes;
            key = hashes?.V1?.ToString() ?? hashes?.V2?.ToString() ?? string.Empty;
        }
        catch
        {
            key = string.Empty;
        }

        return new TorrentRuntimeFact(
            id,
            string.IsNullOrWhiteSpace(key) ? TorrentKey.Empty : new TorrentKey(key),
            manager.Name,
            manager.Torrent?.Size ?? 0,
            manager.SavePath,
            runtime);
    }
}
