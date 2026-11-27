# Текущее устройство кода

<!-- documentation-status-2026-11-27 -->
> Снимок от 2026-11-23, сохранённый до актуализации рабочих карт. Не использовать его как текущую реализацию или новые результаты проверки. Актуальные документы — [здесь](../README.md).

Снимок: 2026-11-23, `265f29b`. Ниже описана реализация по исходникам, без утверждения о прохождении сборки и пользовательских сценариев.

## Модули и зависимости

| Проект | Ответственность | Прямые ссылки на проекты |
| --- | --- | --- |
| `NiTorrent.Domain` | `TorrentDownload`, его статусы и файлы; `SavePath` | Нет |
| `NiTorrent.Application` | Сценарии, интерфейсы хранения/runtime, настройки, координация lifecycle | Domain |
| `NiTorrent.Infrastructure` | Адаптер MonoTorrent, JSON-хранилище, runtime-статусы, настройки | Application |
| `NiTorrent.Presentation` | MVVM, список, preview, карточки и детали, UI-абстракции | Application |
| `NiTorrent.App` | WinUI/XAML, окно, DI, активация, трей, платформенные адаптеры | Infrastructure, Presentation |

Состав указан в `NiTorrent.slnx` (исторический путь: `../../NiTorrent.slnx`), зависимости — в `.csproj` соответствующих проектов. Domain/Application/Infrastructure/Presentation нацелены на `net10.0`; App — на `net10.0-windows10.0.26100.0`. В исходниках проектов указаны MonoTorrent `3.0.2`, CommunityToolkit.Mvvm `8.4.2`, Microsoft.WindowsAppSDK `2.2.0`. Это версии из репозитория, а не проверка доступности или актуальности пакетов.

Точки сборки зависимостей:

- [App.xaml.cs](../../../src/NiTorrent.App/App.xaml.cs): host, application use cases, lifecycle и платформенные адаптеры.
- `Infrastructure/DI/DependencyInjection.cs` (исторический путь: `../../src/NiTorrent.Infrastructure/DI/DependencyInjection.cs`): основные infrastructure-регистрации. Соседний `Infrastructure/DependencyInjection.cs` — устаревшая совместимая обёртка.
- `Presentation/DependencyInjection.cs` (исторический путь: `../../src/NiTorrent.Presentation/DependencyInjection.cs`): view models и фабрика элементов списка.

## Данные и состояния

[TorrentDownload](../../../src/Modules/Torrents/Domain/Torrents/TorrentDownload.cs) содержит `Id`, `InfoHash`, имя, путь, `Status`, `FileEntries`, `Size`. Статусы: `Created`, `Running`, `Paused`, `Completed`, `Failed`, `Deleted`.

Методы доменной модели задают часть переходов: `Start` запрещает запуск Deleted/Completed; `Pause` разрешена только из Running; повторный `MarkDeleted` допустим. При этом `Status` и список файлов имеют публичные setters: переходы через методы не являются единственным способом изменить объект.

У runtime отдельное описание: [TorrentRuntimeStatus](../../../src/Modules/Torrents/Contracts/Torrents/DTo/TorrentRuntimeStatus.cs) и [TorrentLifecycleState](../../../src/Modules/Torrents/Contracts/Torrents/Enum/TorrentLifecycleState.cs). Сохранённый статус и состояние движка нельзя считать одним понятием.

## Запуск, восстановление и завершение

1. `Program` (исторический путь: `../../src/NiTorrent.App/Program.cs`) настраивает WinRT и единственный экземпляр приложения; вторичная активация перенаправляется основному экземпляру.
2. [App](../../../src/NiTorrent.App/App.xaml.cs) собирает host и передаёт управление [NiTorrentApplication](../../../src/NiTorrent.App/Services/AppLifecycle/NiTorrentApplication.cs).
3. `AppLifecycleCoordinator` (исторический путь: `../../src/NiTorrent.Application/AppLifecycleCoordinator.cs`) последовательно выполняет задачи по `Stage`, затем `Order`.
4. Этапы: Bootstrap → Core → Shell → Restore → Background → Ready. Настройки загружаются на Bootstrap; репозиторий и движок — на Core; окно создаётся на Shell.
5. `RestoreSessionLifecycleTask` (исторический путь: `../../src/NiTorrent.App/Services/AppLifecycle/RestoreSessionLifecycleTask.cs`) вызывает lifecycle движка, далее [RestoreSessionUseCase](../../../src/Modules/Torrents/Application/Torrents/UseCase/RestoreSessionUseCase.cs) добавляет сохранённые торренты в runtime и запускает записи со статусом Running.
6. Восстановление дополнительно запрашивает `TorrentViewModel.TorrentLoading`. `TorrentEngineLifecycle` использует gate и флаг завершённого восстановления, чтобы ограничить повторное выполнение.
7. Для остановки предусмотрены отдельные задачи запрета новых операций, остановки движка и flush. Координатор учитывает `ShutdownOrder`, иначе обратный порядок успешно запущенных задач; для отдельных шагов доступны таймауты.

По этому порядку Core завершается до показа Shell. Старое требование показывать окно до полной готовности движка нужно сверять отдельно; отзывчивость здесь не измерялась.

## Добавление

Основной путь из [TorrentViewModel](../../../src/Modules/Desktop/Presentation/Features/Torrents/TorrentViewModel.cs): выбор `.torrent` либо ввод magnet → `PreviewTorrentContentsUseCase` → preview-диалог → `CreateTorrentDownloadUseCase` → элемент списка.

[TorrentMetadataProvider](../../../src/Modules/Torrents/Engines/MonoTorrent/TorrentMetadataProvider.cs) получает байты из файла или скачивает metadata для magnet. Оба варианта преобразуются в metadata с источником `TorrentBytes`.

[CreateTorrentDownloadUseCase](../../../src/Modules/Torrents/Application/Torrents/UseCase/CreateTorrentDownloadUseCase.cs) повторно получает metadata, проверяет наличие InfoHash, создаёт модель, сохраняет запись, добавляет её в runtime, применяет выбор файлов, сохраняет Running и вызывает запуск движка. Между этими действиями есть несколько отдельных операций с побочными эффектами; общей транзакции в методе нет.

## Управление торрентом

| Действие | Текущая последовательность |
| --- | --- |
| [Start](../../../src/Modules/Torrents/Application/Torrents/UseCase/StartTorrentUseCase.cs) | Прочитать запись → изменить локальную модель → runtime Start → сохранить запись |
| [Pause](../../../src/Modules/Torrents/Application/Torrents/UseCase/PauseTorrentUseCase.cs) | Прочитать запись → изменить локальную модель → runtime Pause → сохранить запись |
| [Delete](../../../src/Modules/Torrents/Application/Torrents/UseCase/DeleteTorrentUseCase.cs) | Прочитать запись → MarkDeleted → runtime Remove → удалить запись из репозитория |

[ITorrentRuntimeGateway](../../../src/Modules/Torrents/Application/Torrents/Abstract/ITorrentRuntimeGateway.cs) — seam между сценариями и движком, [TorrentRuntimeGateway](../../../src/Modules/Torrents/Engines/MonoTorrent/TorrentRuntimeGateway.cs) — его адаптер. [TorrentRuntimeWorkGate](../../../src/Modules/Torrents/Engines/MonoTorrent/TorrentRuntimeWorkGate.cs) запрещает новые операции при завершении; это не очередь ожидания готовности движка.

## Чтение и интерфейс

[GetTorrentListQuery](../../../src/Modules/Torrents/Application/Torrents/Queries/GetTorrentListQuery.cs) возвращает список доменных объектов из репозитория. Отдельной проекции `TorrentListItemReadModel`, описанной в старых документах, в этом пути нет.

[InMemoryTorrentRuntimeStateSource](../../../src/Modules/Torrents/Infrastructure/Torrents/InMemoryTorrentRuntimeStateSource.cs) опрашивает provider при наличии подписчиков с задержкой в одну секунду между циклами. `TorrentViewModel` подписывается на изменения и отправляет обновления через `IUiDispatcher`.

Страница деталей и её view model присутствуют, но наличие XAML и полей не подтверждает полноту функции: см. [наблюдения](../OBSERVATIONS.md).

## Хранение и настройки

[JsonTorrentRepository](../../../src/Modules/Torrents/Infrastructure/Torrents/JsonTorrentRepository.cs) хранит записи в `Torrents/torrent_downloads.json`, пути разрешает через `IAppStorageService`. Записывает Id, InfoHash, имя, путь, статус, выбор файлов и источник. Для TorrentBytes используется Base64 в JSON; для TorrentFile предусмотрена локальная копия файла. Сохранения внутри экземпляра репозитория ограничены `SemaphoreSlim`.

[AppStorageService](../../../src/NiTorrent.App/Services/AppStorageService.cs) использует пакетный LocalFolder либо fallback `%LOCALAPPDATA%/NiTorrent/LocalState`. Поэтому перенос данных между packaged/unpackaged вариантами нельзя считать автоматическим.

Настройки загружаются через [SettingsRepository](../../../src/Modules/Torrents/Infrastructure/Settings/SettingsRepository.cs) и `AppJsonSettings`; DI задаёт файл `torrent_settings.json`. [UpdateSettingsUseCase](../../../src/Modules/Torrents/Application/Settings/UpdateSettingsUseCase.cs) сначала вызывает обновление сохранённых настроек и `Current`, затем применение к движку. `SaveAsync` репозитория ставит сохранение в фоновую задачу; `FlushAsync` ожидает её завершения.

## Выпуск

`release.yml` (исторический путь: `../../.github/workflows/release.yml`) описывает публикацию win-x64, ZIP, Inno Setup и GitHub Release. В команде публикации явно отключены AOT и trimming, хотя App.csproj содержит другие значения по умолчанию. В workflow нет шага запуска тестов. Выполнение workflow и наличие успешного релиза в этом анализе не проверялись.
