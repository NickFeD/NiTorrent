namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentEngineLifecycle
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAcceptingWorkAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    void DisposeRuntime();
}
