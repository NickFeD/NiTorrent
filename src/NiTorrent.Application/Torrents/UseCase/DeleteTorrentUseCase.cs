using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class DeleteTorrentUseCase(
    ITorrentRepository repository,
    ITorrentRuntimeGateway runtimeGateway)
{
    private readonly ITorrentRepository _repository = repository;
    private readonly ITorrentRuntimeGateway _runtimeGateway = runtimeGateway;

    public async Task<DeleteTorrentResponse> ExecuteAsync(
        DeleteTorrentCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var download = await _repository.GetByIdAsync(command.TorrentId, ct);
        if (download is null)
            throw new InvalidOperationException("Torrent not found.");

        download.MarkDeleted();

        await _runtimeGateway.RemoveAsync(download.Id, command.DeleteFiles, ct);

        await _repository.DeleteAsync(download.Id, ct);

        return new DeleteTorrentResponse(download.Id);
    }
}
