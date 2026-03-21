using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentSpeedSummaryService
{
    TorrentSpeedSummary Build(IReadOnlyList<TorrentSnapshot> snapshots);
}
