using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentEntrySettingsRuntimeApplier
{
    Task ApplyAsync(TorrentId torrentId, TorrentEntrySettings settings, CancellationToken ct = default);
}
