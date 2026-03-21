using NiTorrent.Application.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Transitional engine status service that starts the engine through infrastructure startup components
/// instead of delegating to the legacy ITorrentService facade.
/// </summary>
public sealed class EngineBackedTorrentEngineStatusService : ITorrentEngineStatusService, IDisposable
{
    private readonly TorrentEventOrchestrator _eventOrchestrator;
    private readonly TorrentStartupCoordinator _startupCoordinator;
    private readonly TorrentRuntimeContext _runtimeContext;
    private readonly TorrentNotifier _notifier;

    public EngineBackedTorrentEngineStatusService(
        TorrentEventOrchestrator eventOrchestrator,
        TorrentStartupCoordinator startupCoordinator,
        TorrentRuntimeContext runtimeContext,
        TorrentNotifier notifier)
    {
        _eventOrchestrator = eventOrchestrator;
        _startupCoordinator = startupCoordinator;
        _runtimeContext = runtimeContext;
        _notifier = notifier;

        _eventOrchestrator.Loaded += OnLoaded;
    }

    public event Action? Ready;

    public bool IsReady => _startupCoordinator.IsReady;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _eventOrchestrator.PublishCachedAsync(ct).ConfigureAwait(false);

        try
        {
            await _startupCoordinator
                .EnsureStartedAsync(_runtimeContext.OperationGate, _runtimeContext.CommandQueue, _eventOrchestrator.RaiseLoaded, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _notifier.NotifyAsync("Ошибка запуска торрент-движка", $"Не удалось запустить торрент-движок.\n\n{ex.Message}").ConfigureAwait(false);
            throw;
        }
    }

    private void OnLoaded()
        => Ready?.Invoke();

    public void Dispose()
    {
        _eventOrchestrator.Loaded -= OnLoaded;
    }
}
