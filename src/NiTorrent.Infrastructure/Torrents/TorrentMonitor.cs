using Microsoft.Extensions.Hosting;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Restore;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentMonitor : BackgroundService
{
    private readonly ITorrentReadModelFeed _readModelFeed;
    private readonly ISyncTorrentCollectionFromRuntimeWorkflow _syncRuntimeWorkflow;

    public TorrentMonitor(
        ITorrentReadModelFeed readModelFeed,
        ISyncTorrentCollectionFromRuntimeWorkflow syncRuntimeWorkflow)
    {
        _readModelFeed = readModelFeed;
        _syncRuntimeWorkflow = syncRuntimeWorkflow;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(ct) && !ct.IsCancellationRequested)
        {
            try
            {
                await _syncRuntimeWorkflow.ExecuteAsync(ct).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Background sync is best-effort; feed refresh still gives the latest query projection.
            }
            catch (IOException)
            {
                // Background sync is best-effort; feed refresh still gives the latest query projection.
            }

            _readModelFeed.Refresh();
        }
    }
}
