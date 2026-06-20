namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentEngineLifecycle
{
    Task StartAsync(CancellationToken cancellationToken);

    Task RestoreSessionAsync(CancellationToken cancellationToken);

    Task StopAcceptingWorkAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task FlushStateAsync(CancellationToken cancellationToken);
}
