using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public interface ITorrentDetailsService
{
    TorrentDetailsReadModel? Get(TorrentId torrentId);
    TorrentEntrySettings GetSettings(TorrentId torrentId);
    Task SaveSettingsAsync(TorrentId torrentId, TorrentEntrySettings settings, CancellationToken ct = default);
}
