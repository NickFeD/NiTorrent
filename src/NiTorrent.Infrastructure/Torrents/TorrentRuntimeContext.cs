namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Shared runtime coordination primitives used by torrent engine startup, read-side refresh and engine command execution.
/// Keeps infrastructure access to the MonoTorrent runtime on the same gate.
/// </summary>
public sealed class TorrentRuntimeContext
{
    public SemaphoreSlim OperationGate { get; } = new(1, 1);
}
