using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentRuntimeMonitor
{
    private readonly ITorrentRuntimeStatusProvider _provider;
    private readonly ITorrentRuntimeStateStore _store;

    public TorrentRuntimeMonitor(
        ITorrentRuntimeStatusProvider provider,
        ITorrentRuntimeStateStore store)
    {
        _provider = provider;
        _store = store;
    }

    public async Task RunAsync(CancellationToken ct)
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
