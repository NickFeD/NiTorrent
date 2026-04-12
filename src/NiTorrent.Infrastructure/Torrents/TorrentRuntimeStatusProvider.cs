using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Infrastructure.Torrents;

public class TorrentRuntimeStatusProvider(TorrentEngineCoordinator coordinator) : ITorrentRuntimeStatusProvider
{
    private readonly TorrentEngineCoordinator _coordinator = coordinator;

    public Task<IReadOnlyList<TorrentRuntimeStatus>> GetAllAsync(CancellationToken ct)
    {
        var keyValues = _coordinator.GetTorrentMap();
        var statuses = keyValues.Keys.ToList().Select(k =>
        {
            var engine = keyValues[k];
            var status = new TorrentRuntimeStatus(k, engine.State.Map(), engine.Error?.ToString(), engine.Monitor.DownloadRate, engine.PartialProgress);

            return status;
        }).ToList();

        return Task.FromResult<IReadOnlyList<TorrentRuntimeStatus>>(statuses);
    }

    public Task<TorrentRuntimeStatus?> GetAsync(Guid torrentId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
