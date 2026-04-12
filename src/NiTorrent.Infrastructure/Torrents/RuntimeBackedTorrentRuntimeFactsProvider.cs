using MonoTorrent;
using MonoTorrent.Client;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;
using NiTorrent.Application.Torrents.Enum;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Runtime facts provider backed directly by the active MonoTorrent managers.
/// It exposes runtime facts and raises invalidation when runtime changes.
/// </summary>
public sealed class RuntimeBackedTorrentRuntimeFactsProvider : ITorrentRuntimeFactsProvider, IDisposable
{
    private const string UserSafeRuntimeErrorMessage = "Ошибка работы торрент-движка.";

    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly TorrentEventOrchestrator _eventOrchestrator;
    private readonly TorrentStartupCoordinator _startupCoordinator;
    private readonly TorrentStableKeyAccessor _stableKeyAccessor;
    private readonly SemaphoreSlim _operationGate;
    private readonly ILogger<RuntimeBackedTorrentRuntimeFactsProvider> _logger;

    public event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;

    public RuntimeBackedTorrentRuntimeFactsProvider(
        TorrentRuntimeRegistry runtimeRegistry,
        TorrentEventOrchestrator eventOrchestrator,
        TorrentStartupCoordinator startupCoordinator,
        TorrentStableKeyAccessor stableKeyAccessor,
        TorrentRuntimeContext runtimeContext,
        ILogger<RuntimeBackedTorrentRuntimeFactsProvider> logger)
    {
        _runtimeRegistry = runtimeRegistry;
        _eventOrchestrator = eventOrchestrator;
        _startupCoordinator = startupCoordinator;
        _stableKeyAccessor = stableKeyAccessor;
        _operationGate = runtimeContext.OperationGate;
        _logger = logger;

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

    private TorrentRuntimeFact MapManager(TorrentId id, TorrentManager manager)
    {
        var stableKey = _stableKeyAccessor.GetStableKey(manager);
        var phase = manager.State switch
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

        var rawRuntimeError = manager.Error;
        if (rawRuntimeError is not null)
        {
            _logger.LogWarning(
                "Runtime error for torrent {TorrentId} ({TorrentName}). Error={RuntimeError}",
                id,
                manager.Name,
                rawRuntimeError.ToString());
        }

        var progress = manager.PartialProgress;
        var runtime = new TorrentRuntimeStateOld(
            new object(),
            progress >= 100.0,
            progress,
            int.MaxValue,
            int.MaxValue,
            rawRuntimeError is null ? null : UserSafeRuntimeErrorMessage,
             true);

        return new TorrentRuntimeFact(
            id,
            string.IsNullOrWhiteSpace(stableKey) ? TorrentKey.Empty : new TorrentKey(stableKey),
            manager.Name,
            manager.Torrent?.Size ?? 0,
            manager.SavePath,
            new());
    }

    public void Dispose()
    {
        _eventOrchestrator.RuntimeInvalidated -= OnRuntimeChanged;
        _eventOrchestrator.Loaded -= OnLoaded;
    }
}
