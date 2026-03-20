using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentWorkflowService
{
    Task<bool> PickAndAddAsync(CancellationToken ct = default);
    Task<bool> AddMagnetAsync(string magnet, CancellationToken ct = default);
    Task<bool> AddTorrentFileWithPreviewAsync(string filePath, CancellationToken ct = default);
    Task StartAsync(TorrentId id, CancellationToken ct = default);
    Task PauseAsync(TorrentId id, CancellationToken ct = default);
    Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default);
    Task OpenFolderAsync(string path, CancellationToken ct = default);
}
