# NiTorrent — README_ARCHITECTURE

## Как читать этот документ
Этот файл описывает **текущую рабочую архитектуру**.

Он не равен:
- `TARGET_ARCHITECTURE.md`
- `TARGET_ARCHITECTURE_V2.md`

За текущий статус отвечает:
- `CURRENT_ARCHITECTURE_STATE.md`

---

## 1. Слои решения

```text
NiTorrent.App
 ├─> NiTorrent.Presentation
 │    └─> NiTorrent.Application
 │         └─> NiTorrent.Domain
 └─> NiTorrent.Infrastructure
      └─> NiTorrent.Application
           └─> NiTorrent.Domain
```

### `NiTorrent.App`
Отвечает за:
- WinUI host;
- окно и shell lifecycle;
- activation;
- platform adapters;
- composition root.

Ключевые файлы:
- `src/NiTorrent.App/App.xaml.cs`
- `src/NiTorrent.App/Services/AppLifecycle/*`
- `src/NiTorrent.App/Services/*`

### `NiTorrent.Presentation`
Отвечает за:
- ViewModel;
- observable UI state;
- UI-команды;
- presentation mapping.

Ключевые файлы:
- `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`
- `src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs`
- `src/NiTorrent.Presentation/Features/Settings/TorrentSettingsViewModel.cs`

### `NiTorrent.Application`
Отвечает за:
- boundary interfaces;
- use case'ы;
- workflow-сервисы.

Ключевые файлы:
- `src/NiTorrent.Application/Abstractions/ITorrentService.cs`
- `src/NiTorrent.Application/Abstractions/ITorrentWorkflowService.cs`
- `src/NiTorrent.Application/Torrents/*`

### `NiTorrent.Domain`
Отвечает за:
- доменные типы snapshot-based рабочего пути;
- и одновременно за seed будущей product-centered модели.

Ключевые файлы:
- `src/NiTorrent.Domain/Torrents/TorrentSnapshot.cs`
- `src/NiTorrent.Domain/Torrents/TorrentStatus.cs`
- `src/NiTorrent.Domain/Torrents/TorrentEntry.cs`
- `src/NiTorrent.Domain/Torrents/Policies/*`

### `NiTorrent.Infrastructure`
Отвечает за:
- интеграцию с MonoTorrent;
- persistence каталога и runtime state;
- мониторинг и публикацию updates;
- применение настроек.

Ключевые файлы:
- `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`
- `src/NiTorrent.Infrastructure/Torrents/TorrentCatalogStore.cs`
- `src/NiTorrent.Infrastructure/Torrents/TorrentUpdatePublisher.cs`
- `src/NiTorrent.Infrastructure/Torrents/TorrentMonitor.cs`

---

## 2. Текущая активная архитектурная ось

### Главный boundary
Текущая система по факту центрируется вокруг:
- `ITorrentService`
- `MonoTorrentService`

Это значит, что именно этот контракт сейчас является главным runtime boundary между UI/application-слоем и движком/пersistence.

### Поверх него уже есть workflow-слой
Поверх `ITorrentService` построен application-level orchestration:
- `ITorrentWorkflowService`
- `TorrentWorkflowService`
- набор use case'ов в `src/NiTorrent.Application/Torrents/`

Это уже полезный шаг к более чистой архитектуре, но он **не заменяет** `ITorrentService` как главный центр работы с торрентами.

---

## 3. Основные потоки

## 3.1. Startup
1. `App.xaml.cs` строит host и регистрирует слои.
2. `IAppStartupService.StartHostAndShellAsync()` стартует host.
3. `IAppStartupService.InitializeTorrentEngineAsync()` вызывает `ITorrentService.InitializeAsync()`.
4. `MonoTorrentService.InitializeAsync()` сначала публикует cached список, потом запускает engine.

## 3.2. Add torrent / magnet
1. UI вызывает `ITorrentWorkflowService`.
2. `TorrentPreviewFlow` получает preview через `ITorrentService.GetPreviewAsync(...)`.
3. После подтверждения вызывается `ITorrentService.AddAsync(...)`.
4. Инфраструктура сохраняет изменения и публикует snapshots.

## 3.3. Обновление списка
1. `TorrentMonitor` каждые 2 секунды вызывает `PublishTorrentUpdates()`.
2. `TorrentEventOrchestrator` собирает merged snapshots.
3. `TorrentViewModel` обновляет `ObservableCollection<TorrentItemViewModel>`.

## 3.4. Shutdown / tray
1. `AppCloseCoordinator` принимает решение hide vs exit.
2. При hide вызывает `ITorrentService.SaveAsync()`.
3. При полном выходе `IAppShutdownCoordinator` завершает host и приложение.

---

## 4. Что важно понимать про текущую архитектуру

### Это не финальная product-centered архитектура
В проекте уже есть `TorrentEntry`, `TorrentIntent`, `DeferredAction`, `AppCloseBehavior` и политики в `Domain`, но они пока не являются главным центром работающей системы.

### Snapshot-модель остаётся активной
Текущий список и update-flow построены вокруг:
- `TorrentSnapshot`
- `ITorrentService.UpdateTorrent`
- `TorrentUpdatePublisher`
- `TorrentCatalogSnapshotSynchronizer`

### Архитектура сейчас — переходная, но рабочая
Сильные стороны текущего состояния:
- слои уже разделены;
- activation вынесен из `App.xaml.cs` в отдельный сервис;
- UI команды идут через workflow/use case слой;
- startup/close-flow стали чище.

Основные ограничения:
- `MonoTorrentService` всё ещё слишком центральный;
- read-side смешивает polling и event-публикацию;
- domain-first модель лежит рядом, но не ведёт рабочий путь.

---

## 5. Что не стоит путать
- `README_ARCHITECTURE.md` — про **сейчас**.
- `TARGET_ARCHITECTURE_V2.md` — про **цель**.
- `TRANSITION_BACKLOG.md` — про **мосты между ними**.
