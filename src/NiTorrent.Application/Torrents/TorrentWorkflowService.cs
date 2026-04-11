using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentWorkflowService(
    PickAndAddTorrentUseCase pickAndAddTorrentUseCase,
    AddMagnetUseCase addMagnetUseCase,
    AddTorrentFileWithPreviewUseCase addTorrentFileWithPreviewUseCase,
    TorrentCommandUseCase torrentCommandUseCase,
    OpenTorrentFolderUseCase openTorrentFolderUseCase) : ITorrentWorkflowService
{
    public Task<bool> PickAndAddAsync(CancellationToken ct = default)
        => pickAndAddTorrentUseCase.ExecuteAsync(ct);

    public Task<bool> AddMagnetAsync(string magnet, CancellationToken ct = default)
        => addMagnetUseCase.ExecuteAsync(magnet, ct);

    public Task<bool> AddTorrentFileWithPreviewAsync(string filePath, CancellationToken ct = default)
        => addTorrentFileWithPreviewUseCase.ExecuteAsync(filePath, ct);

    public Task StartAsync(TorrentId id, CancellationToken ct = default)
        => torrentCommandUseCase.ExecuteAsync(TorrentCommandType.Start, id, ct: ct);

    public Task PauseAsync(TorrentId id, CancellationToken ct = default)
        => torrentCommandUseCase.ExecuteAsync(TorrentCommandType.Pause, id, ct: ct);

    public Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => torrentCommandUseCase.ExecuteAsync(TorrentCommandType.Remove, id, deleteData, ct);

    public Task OpenFolderAsync(string path, CancellationToken ct = default)
        => openTorrentFolderUseCase.ExecuteAsync(path, ct);
}
