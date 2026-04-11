namespace NiTorrent.Application.Torrents.Abstract;

public interface ITorrentRuntimeStatusProvider
{
    Task<TorrentRuntimeStatus?> GetAsync(Guid torrentId, CancellationToken ct);
    Task<IReadOnlyList<TorrentRuntimeStatus>> GetAllAsync(CancellationToken ct);
}
