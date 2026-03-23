# NiTorrent — CURRENT_ARCHITECTURE_STATE

## Назначение
Этот документ — **источник истины о текущем коде**, а не о целевой архитектуре.

Если этот файл конфликтует с:
- `TARGET_ARCHITECTURE.md`
- `TARGET_ARCHITECTURE_V2.md`
- `ARCHITECTURE_TRANSITION_PLAN.md`
- `REFORM_PLAN.md`

то для описания **того, что реально собрано в коде сейчас**, верить нужно **этому файлу**.

---

## 1. Коротко
Текущее приложение уже разложено по слоям, но активный runtime path остаётся **engine-centered**:

- `Presentation` в основном работает через `ITorrentWorkflowService` и `ITorrentService`.
- `Application` содержит use case'ы и сценарные фасады.
- `Infrastructure` держит главный runtime фасад `MonoTorrentService` и вокруг него набор вспомогательных компонентов.
- `Domain` уже содержит заготовки product-centered модели (`TorrentEntry`, `TorrentIntent`, `DeferredAction`, политики), но **эта модель ещё не является центром системы**.

Иными словами:
- **рабочая архитектура** = layered + workflow-oriented поверх `ITorrentService`;
- **целевая архитектура** из `TARGET_ARCHITECTURE_V2.md` = ещё не достигнута полностью.

---

## 2. Активная архитектурная ось

### 2.1. Главный runtime boundary
Главный активный контракт сейчас:
- `src/NiTorrent.Application/Abstractions/ITorrentService.cs`

Его реализация:
- `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`

Через этот контракт сейчас проходят:
- инициализация движка;
- чтение списка и отдельных snapshot'ов;
- preview;
- add/start/pause/stop/remove;
- публикация обновлений списка;
- сохранение и shutdown;
- применение runtime-настроек.

### 2.2. Application workflows
Поверх `ITorrentService` уже есть сценарный слой:
- `ITorrentWorkflowService`
- `TorrentWorkflowService`
- `PickAndAddTorrentUseCase`
- `AddTorrentFileWithPreviewUseCase`
- `AddMagnetUseCase`
- `StartTorrentUseCase`
- `PauseTorrentUseCase`
- `RemoveTorrentUseCase`
- `OpenTorrentFolderUseCase`
- `ApplyTorrentSettingsUseCase`

Этот слой полезен и реально используется UI/activation flow, но он **не заменяет** `ITorrentService` как главный runtime boundary.

---

## 3. Что реально происходит в основных сценариях

## 3.1. Startup
Фактический поток:
1. `App.xaml.cs` строит `Host`.
2. Регистрируются `Infrastructure`, `Presentation`, WinUI-адаптеры и use case'ы.
3. Создаётся `MainWindow` через `IMainWindowLifecycle`.
4. `IAppStartupService.StartHostAndShellAsync()` стартует host и shell-побочные действия.
5. `IAppStartupService.InitializeTorrentEngineAsync()` вызывает `ITorrentService.InitializeAsync()`.
6. `MonoTorrentService.InitializeAsync()` публикует cached список и затем поднимает движок через `TorrentStartupCoordinator`.

## 3.2. File activation `.torrent`
Фактический поток:
1. `AppActivationService` получает activation.
2. Показывается главное окно и при необходимости запускается background initialization.
3. Для каждого `.torrent` вызывается `ITorrentWorkflowService.AddTorrentFileWithPreviewAsync(filePath)`.
4. Preview + confirm + add orchestration проходит через `TorrentPreviewFlow`.

## 3.3. Add / preview
Фактический поток:
1. `TorrentViewModel` или activation вызывает `ITorrentWorkflowService`.
2. `TorrentPreviewFlow` получает preview через `ITorrentService.GetPreviewAsync(...)`.
3. После подтверждения `ITorrentService.AddAsync(...)` добавляет торрент.
4. Инфраструктура обновляет catalog/runtime и публикует snapshots.

## 3.4. Обновление списка
Текущий список строится через snapshot pipeline:
1. `TorrentMonitor` каждые 2 секунды вызывает `ITorrentService.PublishTorrentUpdates()`.
2. `MonoTorrentService` делегирует это в `TorrentEventOrchestrator`.
3. `TorrentUpdatePublisher` строит merged snapshots из:
   - `TorrentCatalogStore` (cached)
   - `TorrentRuntimeRegistry` + `TorrentSnapshotFactory` (live)
4. `TorrentCatalogSnapshotSynchronizer` синхронизирует catalog.
5. `TorrentViewModel` подписана на `ITorrentService.UpdateTorrent` и обновляет `ObservableCollection`.

## 3.5. Settings apply
Текущий поток:
1. `TorrentSettingsViewModel` держит staged-edit состояние формы.
2. По `Save` значения пишутся в `ITorrentPreferences`.
3. `ApplyTorrentSettingsUseCase` вызывает `ITorrentService.ApplySettingsAsync()`.
4. Runtime-настройки применяются через `TorrentSettingsApplier`.

## 3.6. Close / tray / shutdown
Текущий поток:
1. Окно сообщает о close в `AppCloseCoordinator`.
2. Координатор читает `ITorrentPreferences.MinimizeToTrayOnClose`.
3. При hide-to-tray вызывается `ITorrentService.SaveAsync()`.
4. При explicit exit вызывается `IAppShutdownCoordinator`, который останавливает host и завершает приложение.

---

## 4. Что уже подготовлено, но ещё не стало центром системы
В `NiTorrent.Domain` уже лежит product-centered модель:
- `TorrentEntry`
- `TorrentKey`
- `TorrentIntent`
- `TorrentRuntimeState`
- `DeferredAction`
- `AppCloseBehavior`
- `GlobalTorrentSettings`
- политики в `src/NiTorrent.Domain/Torrents/Policies/*`

Но сейчас эти типы:
- почти не участвуют в живом runtime path;
- не являются главным источником истины для списка;
- не заменяют `TorrentSnapshot` и `ShouldRun` в инфраструктурной модели.

Для реального перехода к `TARGET_ARCHITECTURE_V2.md` смотри:
- `TRANSITION_BACKLOG.md`
- `ARCHITECTURE_TRANSITION_PLAN.md`

---

## 5. Самые важные текущие ограничения
1. `MonoTorrentService` остаётся главным runtime фасадом.
2. Read-side остаётся snapshot-based.
3. Update-flow смешивает polling + events + catalog sync.
4. Domain-first модель пока существует рядом с рабочим кодом, а не управляет им.
5. Close behavior ещё представлен через `MinimizeToTrayOnClose`, а не через `AppCloseBehavior`.

---

## 6. Какие документы как читать
- `CURRENT_ARCHITECTURE_STATE.md` — что реально есть в коде сейчас.
- `README_ARCHITECTURE.md` — как устроены текущие слои и активные потоки.
- `ONBOARDING.md` — с чего начинать чтение проекта.
- `PROJECT_MAP.md` — карта модулей и ключевых файлов.
- `REFORM_STATUS_REPORT.md` — текущая точка остановки и честная оценка состояния.
- `TRANSITION_BACKLOG.md` — что ещё остаётся переходным мостом.
- `TARGET_ARCHITECTURE_V2.md` — куда проект планируется довести.
