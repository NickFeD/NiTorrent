using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class StartTorrentUseCase(ITorrentRepository repository, ITorrentSession session)
{
    private readonly ITorrentRepository _repository = repository;
    private readonly ITorrentSession _session = session;

    public async Task<StartTorrentResponse> ExecuteAsync(
        StartTorrentCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var download = await _repository.GetByIdAsync(command.TorrentId, ct) ?? throw new InvalidOperationException("Torrent not found.");

        await _session.SetStateAsync(download.Id, Domain.Torrents.TorrentDownloadStatus.Running, ct);
        download.Status = Domain.Torrents.TorrentDownloadStatus.Running;

        return new StartTorrentResponse(download.Id, download.Status);
    }
}
