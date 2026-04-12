using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public class RestoreSessionUseCase(ITorrentRepository torrentRepository, ITorrentRuntimeGateway torrentRuntimeGateway)
{
    private readonly ITorrentRepository _torrentRepository = torrentRepository;
    private readonly ITorrentRuntimeGateway _torrentRuntimeGateway = torrentRuntimeGateway;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var torrents = await _torrentRepository.GetAllForRestoreAsync(ct);

        foreach (var item in torrents)
        {
            if (await _torrentRuntimeGateway.ExistsByIdAsync(item.Torrent.Id))
                continue;

            await _torrentRuntimeGateway.AddAsync(item.Torrent.Id, item.TorrentSource, item.Torrent.SavePath, ct);
            await _torrentRuntimeGateway.UpdateFileSelectionAsync(item.Torrent.Id, item.Torrent.FileEntries, ct);
            if (item.Torrent.Status == Domain.Torrents.TorrentDownloadStatus.Running)
                await _torrentRuntimeGateway.StartAsync(item.Torrent.Id, ct);

            await Task.Delay(5, ct);
        }
    }

}
