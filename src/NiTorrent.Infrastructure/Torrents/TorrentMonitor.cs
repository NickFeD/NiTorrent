using Microsoft.Extensions.Hosting;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Restore;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentMonitor : BackgroundService
{
    private readonly ITorrentReadModelFeed _readModelFeed;
    private readonly SyncTorrentCollectionFromRuntimeWorkflow _syncRuntimeWorkflow;

    public TorrentMonitor(
        ITorrentReadModelFeed readModelFeed,
        SyncTorrentCollectionFromRuntimeWorkflow syncRuntimeWorkflow)
    {
        _readModelFeed = readModelFeed;
        _syncRuntimeWorkflow = syncRuntimeWorkflow;
    }


    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(ct) && !ct.IsCancellationRequested)
        {
            await _syncRuntimeWorkflow.ExecuteAsync(ct).ConfigureAwait(false);
            _readModelFeed.Refresh();
        }
    }
}
