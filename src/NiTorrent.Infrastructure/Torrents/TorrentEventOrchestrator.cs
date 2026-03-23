using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Infrastructure-only runtime invalidation hub.
/// It only signals that runtime-derived facts have changed so higher layers can refresh
/// projections and synchronize the product-owned collection if needed.
/// </summary>
public sealed class TorrentEventOrchestrator
{
    private readonly BackgroundTaskRunner _backgroundTasks;

    public TorrentEventOrchestrator(BackgroundTaskRunner backgroundTasks)
    {
        _backgroundTasks = backgroundTasks;
    }

    public event Action? Loaded;
    public event Action? Invalidated;

    public void RaiseLoaded()
        => Loaded?.Invoke();

    public Task PublishUpdatesAsync(bool hasEngine, SemaphoreSlim opGate, CancellationToken ct = default)
    {
        Invalidated?.Invoke();
        return Task.CompletedTask;
    }

    public void PublishUpdatesInBackground(bool hasEngine, SemaphoreSlim opGate)
        => _backgroundTasks.Run(PublishUpdatesAsync(hasEngine, opGate, CancellationToken.None), "invalidate-runtime");

    public Task PublishCachedAsync(CancellationToken ct)
    {
        Invalidated?.Invoke();
        return Task.CompletedTask;
    }
}
