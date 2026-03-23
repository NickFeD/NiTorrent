using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentCommandService
{
    Task<TorrentCommandResult> StartAsync(TorrentId id, CancellationToken ct = default);
    Task<TorrentCommandResult> PauseAsync(TorrentId id, CancellationToken ct = default);
    Task<TorrentCommandResult> RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default);
}
