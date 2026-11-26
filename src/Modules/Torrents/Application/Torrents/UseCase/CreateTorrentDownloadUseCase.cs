using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class CreateTorrentDownloadUseCase(ITorrentSession session, ITorrentDownloadFactory downloadFactory, ITorrentRepository torrentRepository, ITorrentMetadataProvider metadataProvider)
{
    private readonly ITorrentMetadataProvider _metadataProvider = metadataProvider;
    private readonly ITorrentRepository _torrentRepository = torrentRepository;
    private readonly ITorrentDownloadFactory _downloadFactory = downloadFactory;
    private readonly ITorrentSession _session = session;

    public async Task<StartedTorrentDownloadResponse> ExecuteAsync(
        StartTorrentDownloadCommand command,
        CancellationToken ct)
    {
        var metadata = await _metadataProvider.ExtractAsync(command.Source, ct);

        var alreadyExists = await _torrentRepository.ExistsByInfoHash(metadata.InfoHash, ct);
        if (alreadyExists)
            throw new Exception("дубликат");

        var download = _downloadFactory.Create(metadata, command.Files, command.DownloadDirectory);


        download.Start();
        await _session.AddAsync(download, metadata.Source, ct);

        return new StartedTorrentDownloadResponse(download);
    }
}
