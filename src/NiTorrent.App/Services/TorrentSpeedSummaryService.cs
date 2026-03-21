using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation;

namespace NiTorrent.App.Services;

public sealed class TorrentSpeedSummaryService : ITorrentSpeedSummaryService
{
    public TorrentSpeedSummary Build(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        long totalDl = 0;
        long totalUl = 0;

        foreach (var snapshot in snapshots)
        {
            totalDl += snapshot.Status.DownloadRateBytesPerSecond;
            totalUl += snapshot.Status.UploadRateBytesPerSecond;
        }

        return new TorrentSpeedSummary(
            SizeFormatter.FormatSpeed(totalDl),
            SizeFormatter.FormatSpeed(totalUl));
    }
}
