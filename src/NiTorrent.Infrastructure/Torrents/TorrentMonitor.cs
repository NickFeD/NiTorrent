using Microsoft.Extensions.Hosting;
using NiTorrent.Application.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentMonitor : BackgroundService
{
    private readonly ITorrentReadModelFeed _readModelFeed;

    public TorrentMonitor(ITorrentReadModelFeed readModelFeed)
    {
        _readModelFeed = readModelFeed;
    }


    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(ct) && !ct.IsCancellationRequested)
        {
            _readModelFeed.Refresh();
        }
    }
}
