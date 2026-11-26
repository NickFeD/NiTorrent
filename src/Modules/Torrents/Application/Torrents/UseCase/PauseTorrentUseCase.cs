using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class PauseTorrentUseCase(ITorrentRepository repository, ITorrentSession session)
{
    private readonly ITorrentRepository _repository = repository;
    private readonly ITorrentSession _session = session;

    public async Task<PauseTorrentResponse> ExecuteAsync(
        PauseTorrentCommand command,
        CancellationToken ct)
    {

        var download = await _repository.GetByIdAsync(command.TorrentId, ct);
        if (download is null)
            throw new InvalidOperationException("Torrent not found.");

        await _session.SetStateAsync(download.Id, Domain.Torrents.TorrentDownloadStatus.Paused, ct);
        download.Status = Domain.Torrents.TorrentDownloadStatus.Paused;

        return new PauseTorrentResponse(download.Id, download.Status);
    }
}
