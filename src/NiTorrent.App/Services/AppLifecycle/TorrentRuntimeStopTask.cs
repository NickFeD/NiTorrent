using NiTorrent.Application;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class TorrentRuntimeStopTask(ITorrentEngineLifecycle torrentEngineLifecycle) : IAppLifecycleTask, IAppLifecycleShutdownStep
{
    private readonly ITorrentEngineLifecycle _torrentEngineLifecycle = torrentEngineLifecycle;

    public string Name => "Stop torrent runtime work";

    public AppStartupStage Stage => AppStartupStage.Ready;

    public int Order => 800;

    public int ShutdownOrder => 500;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _torrentEngineLifecycle.StopAsync(cancellationToken);
}
