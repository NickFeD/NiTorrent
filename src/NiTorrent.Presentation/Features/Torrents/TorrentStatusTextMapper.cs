using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

internal static class TorrentStatusTextMapper
{
    public static string ToUserFacingText(TorrentStatus status)
    {
        var sourceSuffix = status.Source == TorrentStatusSource.Cached ? " (кэш)" : string.Empty;

        return "Запуск движка";
    }
}
