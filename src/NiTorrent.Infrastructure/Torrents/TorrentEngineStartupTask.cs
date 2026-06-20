using NiTorrent.Application;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.Infrastructure.Torrents;

internal class TorrentEngineStartupTask(ITorrentEngineLifecycle torrentEngineLifecycle) : IAppLifecycleTask
{
    private readonly ITorrentEngineLifecycle _torrentEngineLifecycle = torrentEngineLifecycle;

    public string Name => "Start torrent engine runtime";

    public AppStartupStage Stage => AppStartupStage.Core;

    public int Order => 200;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _torrentEngineLifecycle.StartAsync(cancellationToken);

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
