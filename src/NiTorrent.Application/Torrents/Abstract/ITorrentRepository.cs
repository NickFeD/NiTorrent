using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentRepository
{
    Task AddAsync(TorrentDownload download, TorrentSource source, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsByInfoHash(string infoHash, CancellationToken ct);
    Task<List<TorrentDownload>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<StoredTorrent>> GetAllForRestoreAsync(CancellationToken ct);
    Task<TorrentDownload?> GetByIdAsync(Guid torrentId, CancellationToken ct);
    Task LoadingAsync(CancellationToken ct);
    Task UpdateAsync(TorrentDownload download, CancellationToken ct);
}
