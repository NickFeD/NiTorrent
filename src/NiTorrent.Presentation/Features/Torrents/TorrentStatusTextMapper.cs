using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

internal static class TorrentStatusTextMapper
{
    public static string ToUserFacingText(TorrentStatus status)
    {
        var sourceSuffix = status.Source == TorrentStatusSource.Cached ? " (кэш)" : string.Empty;

        return status.Phase switch
        {
            TorrentPhase.EngineStarting => $"Запуск движка{sourceSuffix}",
            TorrentPhase.WaitingForEngine => $"Ожидает запуск движка{sourceSuffix}",
            TorrentPhase.FetchingMetadata => $"Получение метаданных{sourceSuffix}",
            TorrentPhase.Checking => $"Проверка файлов{sourceSuffix}",
            TorrentPhase.Downloading => $"Скачивание{sourceSuffix}",
            TorrentPhase.Seeding => $"Раздача{sourceSuffix}",
            TorrentPhase.Paused => $"Пауза/остановлен{sourceSuffix}",
            TorrentPhase.Stopped => $"Пауза/остановлен{sourceSuffix}",
            TorrentPhase.Error => $"Ошибка{sourceSuffix}",
            _ => $"Неизвестно{sourceSuffix}"
        };
    }
}
