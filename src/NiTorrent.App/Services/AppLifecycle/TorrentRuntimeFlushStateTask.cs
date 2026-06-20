using NiTorrent.Application;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class TorrentRuntimeFlushStateTask(ITorrentEngineLifecycle torrentEngineLifecycle) : IAppLifecycleTask, IAppLifecycleShutdownStep
{
    private readonly ITorrentEngineLifecycle _torrentEngineLifecycle = torrentEngineLifecycle;

    public string Name => "Flush torrent runtime state";

    public AppStartupStage Stage => AppStartupStage.Core;

    public int Order => 300;

    public int ShutdownOrder => 600;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _torrentEngineLifecycle.FlushStateAsync(cancellationToken);
}
