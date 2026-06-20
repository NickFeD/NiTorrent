using NiTorrent.Application;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.Infrastructure.Torrents;

internal class TorrentRepositoryStartupTask(ITorrentRepository torrentRepository) : IAppLifecycleTask
{
    public string Name => "Load torrent repository";

    public AppStartupStage Stage => AppStartupStage.Core;

    public int Order => 100;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => torrentRepository.LoadingAsync(cancellationToken);

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
