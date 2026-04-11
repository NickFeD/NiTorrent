using NiTorrent.Application.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Application-facing engine status service backed by infrastructure startup components.
/// </summary>
public sealed class EngineBackedTorrentEngineStatusService : ITorrentEngineStatusService, IDisposable
{
    private readonly TorrentEventOrchestrator _eventOrchestrator;
    private readonly TorrentStartupCoordinator _startupCoordinator;
    private readonly TorrentRuntimeContext _runtimeContext;

    public EngineBackedTorrentEngineStatusService(
        TorrentEventOrchestrator eventOrchestrator,
        TorrentStartupCoordinator startupCoordinator,
        TorrentRuntimeContext runtimeContext)
    {
        _eventOrchestrator = eventOrchestrator;
        _startupCoordinator = startupCoordinator;
        _runtimeContext = runtimeContext;

        _eventOrchestrator.Loaded += OnLoaded;
    }

    public event Action? Ready;

    public bool IsReady => _startupCoordinator.IsReady;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _startupCoordinator
            .EnsureStartedAsync(_runtimeContext.OperationGate, _eventOrchestrator.RaiseLoaded, ct)
            .ConfigureAwait(false);
    }

    private void OnLoaded()
        => Ready?.Invoke();

    public void Dispose()
    {
        _eventOrchestrator.Loaded -= OnLoaded;
    }
}
