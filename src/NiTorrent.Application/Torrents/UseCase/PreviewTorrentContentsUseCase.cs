using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class PreviewTorrentContentsUseCase(ITorrentMetadataProvider metadataProvider, ITorrentRepository torrentRepository)
{
    private readonly ITorrentMetadataProvider _metadataProvider = metadataProvider;
    private readonly ITorrentRepository _torrentRepository = torrentRepository;

    public async Task<PreviewTorrentContentsResponse> ExecuteAsync(
        PreviewTorrentContentsCommand command,
        CancellationToken ct = default)
    {
        var metadata = await _metadataProvider.ExtractAsync(command.Source, ct);

        var alreadyExists = await _torrentRepository.ExistsByInfoHash(metadata.InfoHash, ct);

        var response = new PreviewTorrentContentsResponse(
            AlreadyExists: alreadyExists,
            Name: metadata.Name,
            InfoHash: metadata.InfoHash,
            TotalSize: metadata.TotalSize,
            Files: metadata.Files
                .ToList());

        return response;
    }
}
