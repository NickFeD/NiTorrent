# Текущее устройство кода

Сверено 2026-11-27. Источник — рабочее дерево, а не состояние коммита или старые workflow-документы. [Снимок до изменений](history/CURRENT_CODE_2026-11-23.md) сохранён отдельно.

## Проекты и время жизни

11 production-проектов сгруппированы в [NiTorrent.slnx](../../NiTorrent.slnx). SDK закреплён в [global.json](../../global.json): .NET 11.0.100. App, Desktop.WinUI и Updates.Windows используют Windows TFM; остальные production-проекты — `net11.0`.

| Расположение | Ответственность |
| --- | --- |
| `src/NiTorrent.App` | Program, host, выбор движка, логирование, платформенные storage-пути, согласованное завершение |
| `src/Modules/Torrents/Contracts` | ITorrentsModule, ITorrentsLifetime, ITorrentSettings, ITorrentObservations и отдельные модели данных |
| `src/Modules/Torrents/Domain` | TorrentDownload, намерения и правила переходов |
| `src/Modules/Torrents/Application` | TorrentSession, сценарии, адаптация внешнего интерфейса модуля |
| `src/Modules/Torrents/Infrastructure` | JSON-каталог, настройки, host-задачи и общий сбор наблюдений; без зависимости от конкретного адаптера движка |
| `src/Modules/Torrents/Engines/MonoTorrent` | MonoTorrent-адаптер; реализации internal, публична точка регистрации |
| `src/Modules/Desktop/Contracts` | Контракты темы, URI и сведений о приложении |
| `src/Modules/Desktop/Presentation` | ViewModel, состояние страницы, управление её наблюдением |
| `src/Modules/Desktop/WinUI` | Окна, XAML, страницы, навигация, активации, трей, платформенные UI-адаптеры |
| `src/Modules/Updates/{Contracts,Windows}` | Интерфейс обновлений и перенесённый адаптер DevWinUI; новый механизм обновления не добавлялся |
| `tests` | Два активных и три исторических тестовых проекта |

Некоторые пространства имён и имена сборок сохранены. Например, простые TorrentDownloadStatus/TorrentFileEntry имеют прежний namespace, но принадлежат Contracts. По одному namespace нельзя определять зависимость от Domain. UI получает TorrentListItem, а не живой TorrentDownload.

## Точки сборки

- [App](../../src/NiTorrent.App/App.xaml.cs): AddNiTorrentInfrastructure, явный AddMonoTorrentEngine, Desktop и Updates.
- [Torrents registration](../../src/Modules/Torrents/Infrastructure/DI/DependencyInjection.cs): состояние модуля и host-задачи.
- [MonoTorrent registration](../../src/Modules/Torrents/Engines/MonoTorrent/DependencyInjection.cs): выбранный адаптер. Headless-host должен зарегистрировать его отдельно.
- [Desktop registration](../../src/Modules/Desktop/WinUI/DependencyInjection.cs) и [Presentation registration](../../src/Modules/Desktop/Presentation/DependencyInjection.cs).

## Запуск и завершение

Program обрабатывает единственный экземпляр и перенаправление активаций. NiTorrentApplication запускает host, затем оболочку. StorageHostedService загружает настройки и каталог; TorrentSessionHostedService запускает отслеживаемый исполнитель сессии; наблюдение ожидает потребителей. Создание движка и подготовка торрентов не требуют окна. Завершение host не означает, что при старте вся коллекция уже восстановлена.

[TorrentSession](../../src/Modules/Torrents/Application/Torrents/TorrentSession.cs) сохраняет актуальное намерение, планирует подготовку и перечитывает его после длительных операций. Действия пользователя получают приоритет с защитой обычной очереди от голодания. Ошибка записи торрента изолируется; повтор относится к этой записи. Общий движок имеет ограниченный повтор старта.

Exit закрывает приём команд, отменяет startup/операции, закрывает оболочку и останавливает host в обратном порядке. Общий срок ожидания — 10 секунд; clean marker ставится только после успешной остановки и сохранения. ViewModel не запускает восстановление и не владеет временем жизни движка.

## Добавление и управление

[ITorrentsModule](../../src/Modules/Torrents/Contracts/ITorrentsModule.cs) предоставляет Preview/Add/Start/Pause/Delete/SetAll/Retry и чтение коллекции. Для `.torrent` и magnet сначала открывается окно предпросмотра, затем загружаются metadata. Magnet требует доступного движка для получения metadata. После подтверждения создаётся сохранённая запись и начинается подготовка сессией. Восстановление через новый источник остаётся будущим сценарием.

[TorrentPreviewViewModel](../../src/Modules/Desktop/Presentation/Features/Torrents/TorrentPreviewViewModel.cs) получает metadata через асинхронную `LoadCommand`; индикатор связан с `IsRunning`. Ошибка остаётся в окне с возможностью повтора. Закрытие отменяет запрос, поздний результат игнорируется. Готовое дерево сохраняет выбор файлов; подтверждение недоступно без выбранных файлов/папки или для уже добавленного торрента. Этот UI-проход не меняет повторное извлечение metadata при Add и не доказывает отсутствие библиотечного кэша на диске.

Проверка этого изменения 2026-11-27: полная managed x64 Debug-сборка без ошибок; 76 lifecycle + 13 architecture passed, failed/skipped 0. Сохранились три nullable-предупреждения; NuGet также сообщил о недоступности источника проверки уязвимостей. Реальное окно и сетевой magnet в этом проходе не проверялись. TRX: `artifacts/preview/lifecycle.trx`, `architecture-current.trx`. Для `--no-build` архитектурных тестов использована папка `bin/Debug`, созданная сборкой решения; `bin/x64/Debug` содержала старую сборку с прежними правилами.

Проверки модели окна — [TorrentPreviewLoadingTests](../../tests/NiTorrent.Lifecycle.Tests/TorrentPreviewLoadingTests.cs):

| Requirement | Evidence |
| --- | --- |
| Ожидание через команду, выбор файлов и папки | `LoadingCommandShowsWaitingThenPreservesFileSelectionAndFolder` |
| Ошибка и повтор | `MetadataFailureStaysInWindowAndRetryLoadsPreview` |
| Отмена при закрытии | `ClosingCancelsMetadataRequestWithoutShowingError` |
| Игнорирование позднего ответа | `MetadataArrivingAfterCloseCannotPopulatePreview` |
| Объяснение дубликата и запрет подтверждения | `AlreadyAddedTorrentExplainsWhyConfirmationIsUnavailable` |

Start/Pause сначала сохраняют намерение; исполнитель применяет актуальный выбор по готовности записи. Удаление без файлов не требует подготовки менеджера. Удаление с файлами сохраняет Deleted/DeleteFiles и завершается через движок без запуска передачи; неуспешная операция остаётся доступной для повтора и перезапуска.

## Наблюдение

[ITorrentObservations](../../src/Modules/Torrents/Contracts/TorrentObservation.cs) возвращает независимый IDisposable. Области — общие скорости, коллекция, конкретный торрент. Пиры/трекеры в текущем адаптере наблюдения возвращают Unsupported с объяснением; это не ограничение самой библиотеки MonoTorrent.

Один сборщик обслуживает потребителей примерно раз в секунду. Последняя отписка прекращает дополнительный сбор; обязательная работа ядра продолжается. У каждого потребителя последовательная доставка и один последний ожидающий снимок. При ошибке сохраняются последние значения с Disconnected; после успешного чтения состояние восстанавливается автоматически. История новым механизмом не создаётся.

[TorrentPageObservation](../../src/Modules/Desktop/Presentation/Features/Torrents/TorrentPageObservation.cs) отключает страницу при скрытии в трей, сохраняет локальный выбор и подключает её при возврате. Трей имеет собственную подписку Summary. У текущей страницы деталей часть наполнения остаётся заглушкой: сам факт существования XAML не означает работу peers/графика/расширенных настроек.

## Хранение и выпуск

[JsonTorrentRepository](../../src/Modules/Torrents/Infrastructure/Torrents/JsonTorrentRepository.cs) сохраняет `Torrents/torrent_downloads.json`, в том числе намерение, выбор файлов и источник. TorrentBytes остаются Base64; формат и расположение данных не мигрировали. Записи каталога/настроек используют временный файл и замену; неудачная обычная запись оставляет данные в памяти для следующей попытки. Финальный flush должен сообщить о сбое. Не считать это гарантией отсутствия потерь при аварийном завершении.

Семантика Apply пока не является транзакцией записи и применения: настройки/Current обновляются до применения к движку, автоматического отката нет. Подробности и вопросы — [OBSERVATIONS](OBSERVATIONS.md), [QUESTIONS](QUESTIONS.md).

Все тестовые проекты находятся в [tests](../../tests). Активные Lifecycle и Architecture дают 73 + 13 тестов в последних прогонах; три проекта прежней модели не активированы. [Architecture workflow](../../.github/workflows/architecture.yml) проверяет PR, [release workflow](../../.github/workflows/release.yml) зависит от успешной проверки. Удалённый запуск CI и required checks ветки не настраивались в этой сессии.

Точные результаты, команды и ограничения — [отчёт первого этапа](MODULAR_MONOLITH_IMPLEMENTED_2026-11-27.md).
