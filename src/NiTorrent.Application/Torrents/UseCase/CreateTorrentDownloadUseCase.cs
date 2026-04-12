using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class CreateTorrentDownloadUseCase(ITorrentRuntimeGateway downloadEngine, ITorrentDownloadFactory downloadFactory, ITorrentRepository torrentRepository, ITorrentMetadataProvider metadataProvider)
{
    private readonly ITorrentMetadataProvider _metadataProvider = metadataProvider;
    private readonly ITorrentRepository _torrentRepository = torrentRepository;
    private readonly ITorrentDownloadFactory _downloadFactory = downloadFactory;
    private readonly ITorrentRuntimeGateway _downloadEngine = downloadEngine;

    public async Task<StartedTorrentDownloadResponse> ExecuteAsync(
        StartTorrentDownloadCommand command,
        CancellationToken ct)
    {
        var metadata = await _metadataProvider.ExtractAsync(command.Source, ct);

        var alreadyExists = await _torrentRepository.ExistsByInfoHash(metadata.InfoHash, ct);
        if (alreadyExists)
            throw new Exception("дубликат");

        var download = _downloadFactory.Create(metadata, command.Files, command.DownloadDirectory);


        await _torrentRepository.AddAsync(download, metadata.Source, ct);

        await _downloadEngine.AddAsync(download.Id, metadata.Source, download.SavePath, ct);
        await _downloadEngine.UpdateFileSelectionAsync(download.Id, download.FileEntries, ct);

        download.Start();

        await _torrentRepository.UpdateAsync(download, ct);

        await _downloadEngine.StartAsync(download.Id, ct);

        return new StartedTorrentDownloadResponse(download);
    }
}
