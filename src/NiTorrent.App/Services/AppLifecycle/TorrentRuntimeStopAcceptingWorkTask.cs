using NiTorrent.Application;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class TorrentRuntimeStopAcceptingWorkTask(ITorrentEngineLifecycle torrentEngineLifecycle) : IAppLifecycleTask, IAppLifecycleShutdownStep
{
    private readonly ITorrentEngineLifecycle _torrentEngineLifecycle = torrentEngineLifecycle;

    public string Name => "Stop accepting torrent work";

    public AppStartupStage Stage => AppStartupStage.Ready;

    public int Order => 900;

    public int ShutdownOrder => 100;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _torrentEngineLifecycle.StopAcceptingWorkAsync(cancellationToken);
}
