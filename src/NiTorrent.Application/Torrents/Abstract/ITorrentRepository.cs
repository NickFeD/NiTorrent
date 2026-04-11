using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentRepository
{
    Task AddAsync(TorrentDownload download, TorrentSource source, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsByInfoHash(string infoHash, CancellationToken ct);
    Task<TorrentDownload?> GetByIdAsync(Guid torrentId, CancellationToken ct);
    Task UpdateAsync(TorrentDownload download, CancellationToken ct);
}
