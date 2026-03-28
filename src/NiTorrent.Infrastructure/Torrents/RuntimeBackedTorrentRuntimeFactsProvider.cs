using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Runtime facts provider backed directly by the active MonoTorrent managers.
/// It exposes runtime facts and raises invalidation when runtime changes.
/// </summary>
public sealed class RuntimeBackedTorrentRuntimeFactsProvider : ITorrentRuntimeFactsProvider, IDisposable
{
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly TorrentEventOrchestrator _eventOrchestrator;
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
        _eventOrchestrator = eventOrchestrator;
        _startupCoordinator = startupCoordinator;
        _operationGate = runtimeContext.OperationGate;

        _eventOrchestrator.RuntimeInvalidated += OnRuntimeChanged;
        _eventOrchestrator.Loaded += OnLoaded;
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
        var phase = manager.State switch
        {
            TorrentState.Metadata => TorrentPhase.FetchingMetadata,
            TorrentState.Hashing or TorrentState.FetchingHashes => TorrentPhase.Checking,
            TorrentState.Downloading => TorrentPhase.Downloading,
            TorrentState.Seeding => TorrentPhase.Seeding,
            TorrentState.Paused => TorrentPhase.Paused,
            TorrentState.Stopped => TorrentPhase.Stopped,
            TorrentState.Error => TorrentPhase.Error,
            _ => TorrentPhase.Unknown
        };

        var progress = manager.PartialProgress;
        var runtime = new TorrentRuntimeState(
            TorrentLifecycleStateMapper.FromPhase(phase),
            progress >= 100.0,
            progress,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            manager.Error?.ToString(),
            isEngineBacked: true);

        return new TorrentRuntimeFact(
            id,
            string.IsNullOrWhiteSpace(GetStableKey(manager)) ? TorrentKey.Empty : new TorrentKey(GetStableKey(manager)),
            manager.Name,
            manager.Torrent?.Size ?? 0,
            manager.SavePath,
            runtime);
    }

    private static string GetStableKey(TorrentManager manager)
    {
        try
        {
            var infoHashes = manager.InfoHashes;
            var v1 = infoHashes?.V1;
            if (v1 is not null)
                return v1.ToString() ?? string.Empty;

            var v2 = infoHashes?.V2;
            return v2?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        _eventOrchestrator.RuntimeInvalidated -= OnRuntimeChanged;
        _eventOrchestrator.Loaded -= OnLoaded;
    }
}
