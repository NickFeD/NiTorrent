using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Query;

public class GetTorrentListQuery(ITorrentRepository torrentDownloadRepository)
{
    private readonly ITorrentRepository _torrentDownloadRepository = torrentDownloadRepository;
    public Task<List<TorrentDownload>> ExecuteAsync(CancellationToken ct)
    {
        return _torrentDownloadRepository.GetAllAsync(ct);
    }
}
