namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Thin runtime event hub for infrastructure.
/// It only signals lifecycle and invalidation, and never publishes UI-facing models.
/// </summary>
public sealed class TorrentEventOrchestrator
{
    public event Action? Loaded;
    public event Action? RuntimeInvalidated;

    public void RaiseLoaded() => Loaded?.Invoke();

    public void InvalidateRuntime() => RuntimeInvalidated?.Invoke();
}
