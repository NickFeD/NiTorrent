namespace NiTorrent.Infrastructure.Torrents;

internal sealed class TorrentRuntimeWorkGate
{
    private int _acceptingWork = 1;

    public bool IsAcceptingWork => Volatile.Read(ref _acceptingWork) == 1;

    public void StopAcceptingWork()
        => Interlocked.Exchange(ref _acceptingWork, 0);

    public void ThrowIfNotAcceptingWork()
    {
        if (!IsAcceptingWork)
            throw new InvalidOperationException("Torrent runtime is shutting down and no longer accepts new work.");
    }
}
