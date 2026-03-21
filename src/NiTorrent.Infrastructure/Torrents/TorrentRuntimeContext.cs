namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Shared runtime coordination primitives used during the transition away from the legacy compatibility facade.
/// Keeps engine startup/update/save flows on the same gate and preserves queued user intent until the engine is ready.
/// </summary>
public sealed class TorrentRuntimeContext
{
    public SemaphoreSlim OperationGate { get; } = new(1, 1);
    public TorrentCommandQueue CommandQueue { get; } = new();
}
