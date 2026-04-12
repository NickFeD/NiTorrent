using Microsoft.Extensions.Hosting;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentRuntimeMonitor(
    ITorrentRuntimeStatusProvider provider,
    ITorrentRuntimeStateStore store) : BackgroundService
{
    private readonly ITorrentRuntimeStatusProvider _provider = provider;
    private readonly ITorrentRuntimeStateStore _store = store;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var statuses = await _provider.GetAllAsync(ct);
                _store.Update(statuses);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
}
