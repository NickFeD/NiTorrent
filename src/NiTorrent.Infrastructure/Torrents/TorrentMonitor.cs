using Microsoft.Extensions.Hosting;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentMonitor : BackgroundService
{
    private readonly ITorrentService _torrentService;

    public TorrentMonitor(ITorrentService torrentService)
    {
        _torrentService = torrentService;
    }


    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(ct) && !ct.IsCancellationRequested)
        {
            _torrentService.UpdateTorrent();
        }
    }
}
