using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentSourceStore
{
    Task SaveAsync(TorrentId id, TorrentKey key, byte[] torrentBytes, CancellationToken ct = default);
    Task<byte[]?> TryLoadAsync(TorrentId id, TorrentKey key, CancellationToken ct = default);
    Task DeleteAsync(TorrentId id, CancellationToken ct = default);
}
