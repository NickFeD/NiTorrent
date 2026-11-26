using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class DeleteTorrentUseCase(
    ITorrentRepository repository,
    ITorrentSession session)
{
    private readonly ITorrentRepository _repository = repository;
    private readonly ITorrentSession _session = session;

    public async Task<DeleteTorrentResponse> ExecuteAsync(
        DeleteTorrentCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var download = await _repository.GetByIdAsync(command.TorrentId, ct);
        if (download is null)
            throw new InvalidOperationException("Torrent not found.");

        await _session.DeleteAsync(download.Id, command.DeleteFiles, ct);

        return new DeleteTorrentResponse(download.Id);
    }
}
