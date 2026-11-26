using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Infrastructure.Torrents;

internal class TorrentRuntimeStatusProvider(TorrentEngineCoordinator coordinator, ITorrentRepository repository, ITorrentSession session) : ITorrentRuntimeStatusProvider, ITorrentSummaryProvider
{
    private readonly TorrentEngineCoordinator _coordinator = coordinator;

    public Task<TorrentTransferSummary> GetSummaryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var managers = _coordinator.GetTorrentMap().Values;
        return Task.FromResult(new TorrentTransferSummary(managers.Sum(m => m.Monitor.DownloadRate), managers.Sum(m => m.Monitor.UploadRate)));
    }

    public async Task<IReadOnlyList<TorrentRuntimeStatus>> GetAllAsync(CancellationToken ct)
    {
        var keyValues = _coordinator.GetTorrentMap();
        var preparation = session.GetPreparations();
        var items = await repository.GetAllAsync(ct).ConfigureAwait(false);
        var statuses = items.Select(item =>
        {
            var k = item.Id;
            preparation.TryGetValue(k, out var p);
            if (session.EngineError is { } error)
                return new TorrentRuntimeStatus(k, Application.Torrents.Enum.TorrentLifecycleState.Error, error, 0, 0, 0) { DesiredStatus = item.Status };
            if (p?.State == TorrentPreparationState.Failed)
                return new TorrentRuntimeStatus(k, Application.Torrents.Enum.TorrentLifecycleState.Error, p.Error, 0, 0, 0) { DesiredStatus = item.Status };
            if (item.Status == Domain.Torrents.TorrentDownloadStatus.Deleted)
                return new TorrentRuntimeStatus(k, Application.Torrents.Enum.TorrentLifecycleState.Deleting, null, 0, 0, 0) { DesiredStatus = item.Status };
            if (p?.State != TorrentPreparationState.Ready || !keyValues.TryGetValue(k, out var engine))
                return new TorrentRuntimeStatus(k, Application.Torrents.Enum.TorrentLifecycleState.Preparing, null, 0, 0, 0) { DesiredStatus = item.Status };
            var status = new TorrentRuntimeStatus(
                k,
                engine.State.Map(),
                engine.Error?.ToString(),
                engine.Monitor.DownloadRate,
                engine.Monitor.UploadRate,
                engine.PartialProgress);

            return status with { DesiredStatus = item.Status };
        }).ToList();

        return statuses;
    }

    public async Task<TorrentRuntimeStatus?> GetAsync(Guid torrentId, CancellationToken ct)
    {
        return (await GetAllAsync(ct).ConfigureAwait(false)).FirstOrDefault(s => s.TorrentId == torrentId);
    }
}
