using NiTorrent.Application.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentSourcePreparationService
{
    Task<PreparedTorrentSource> PrepareAsync(TorrentSource source, CancellationToken ct = default);
}
