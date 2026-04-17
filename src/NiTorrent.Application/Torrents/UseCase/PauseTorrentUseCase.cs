using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class PauseTorrentUseCase(ITorrentRepository repository, ITorrentRuntimeGateway runtimeGateway)
{
    private readonly ITorrentRepository _repository = repository;
    private readonly ITorrentRuntimeGateway _runtimeGateway = runtimeGateway;

    public async Task<PauseTorrentResponse> ExecuteAsync(
        PauseTorrentCommand command,
        CancellationToken ct)
    {

        var download = await _repository.GetByIdAsync(command.TorrentId, ct);
        if (download is null)
            throw new InvalidOperationException("Torrent not found.");

        download.Pause();

        await _runtimeGateway.PauseAsync(download.Id, ct);

        await _repository.UpdateAsync(download, ct);

        return new PauseTorrentResponse(download.Id, download.Status);
    }
}
