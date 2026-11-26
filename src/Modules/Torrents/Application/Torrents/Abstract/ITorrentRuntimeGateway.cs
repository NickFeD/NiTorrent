using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentRuntimeGateway
{
    Task AddAsync(Guid id, TorrentSource source, string savePath, CancellationToken ct);

    Task<bool> ExistsByIdAsync(Guid id);
    Task StartAsync(Guid id, CancellationToken ct);
    Task UpdateFileSelectionAsync(Guid id, List<TorrentFileEntry> torrentFiles, CancellationToken ct);
    Task PauseAsync(Guid torrentId, CancellationToken ct);
    Task RemoveAsync(Guid torrentId, bool deleteFiles, CancellationToken ct);
}
