# NiTorrent — ONBOARDING

## Для кого этот документ
Для разработчика, который впервые открыл проект и хочет быстро понять:
- как устроен текущий рабочий код;
- где находится главный runtime path;
- какие документы про текущее состояние, а какие про цель.

---

## 1. Что прочитать сначала
Читать в таком порядке:

1. `docs/project-map/CURRENT_ARCHITECTURE_STATE.md`
2. `docs/project-map/README_ARCHITECTURE.md`
3. `docs/project-map/PROJECT_MAP.md`
4. `docs/project-map/TRANSITION_BACKLOG.md`
5. `docs/project-map/TARGET_ARCHITECTURE_V2.md`

Такой порядок важен, потому что:
- сначала нужно понять **что есть сейчас**;
- потом — **какие мосты ещё живут**;
- и только потом — **куда проект планируется довести**.

---

## 2. Что сейчас является центром системы
Не `TorrentEntry` и не целевая product repository.

Текущий рабочий центр системы:
- `src/NiTorrent.Application/Abstractions/ITorrentService.cs`
- `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`

Это главный boundary для:
- startup;
- preview;
- add/start/pause/remove;
- чтения списка;
- save/shutdown;
- runtime settings apply.

Поверх него уже есть workflow-слой:
- `src/NiTorrent.Application/Abstractions/ITorrentWorkflowService.cs`
- `src/NiTorrent.Application/Torrents/TorrentWorkflowService.cs`

Но это **второй сценарный слой**, а не замена `ITorrentService`.

---

## 3. Как читать код по сценариям

### Сценарий A. Startup
Читай так:
1. `src/NiTorrent.App/App.xaml.cs`
2. `src/NiTorrent.App/Services/AppLifecycle/AppStartupService.cs`
3. `src/NiTorrent.Application/Abstractions/ITorrentService.cs`
4. `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`
5. `src/NiTorrent.Infrastructure/Torrents/TorrentStartupCoordinator.cs`

### Сценарий B. File activation `.torrent`
1. `src/NiTorrent.App/Services/AppLifecycle/AppActivationService.cs`
2. `src/NiTorrent.Application/Abstractions/ITorrentWorkflowService.cs`
3. `src/NiTorrent.Application/Torrents/AddTorrentFileWithPreviewUseCase.cs`
4. `src/NiTorrent.Application/Torrents/TorrentPreviewFlow.cs`
5. `src/NiTorrent.Infrastructure/Torrents/TorrentSourceResolver.cs`
6. `src/NiTorrent.Infrastructure/Torrents/TorrentAddExecutor.cs`

### Сценарий C. Обновление списка
1. `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`
2. `src/NiTorrent.Application/Abstractions/ITorrentService.cs`
3. `src/NiTorrent.Infrastructure/Torrents/TorrentMonitor.cs`
4. `src/NiTorrent.Infrastructure/Torrents/TorrentEventOrchestrator.cs`
5. `src/NiTorrent.Infrastructure/Torrents/TorrentUpdatePublisher.cs`
6. `src/NiTorrent.Infrastructure/Torrents/TorrentCatalogStore.cs`

### Сценарий D. Close / tray / shutdown
1. `src/NiTorrent.App/Services/AppLifecycle/AppCloseCoordinator.cs`
2. `src/NiTorrent.App/Services/AppLifecycle/AppShutdownCoordinator.cs`
3. `src/NiTorrent.Application/Abstractions/ITorrentPreferences.cs`
4. `src/NiTorrent.Infrastructure/Settings/JsonTorrentPreferences.cs`
5. `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`

---

## 4. Что в Domain уже есть, но ещё не управляет приложением
В `NiTorrent.Domain` уже появились:
- `TorrentEntry`
- `TorrentIntent`
- `TorrentKey`
- `TorrentRuntimeState`
- `DeferredAction`
- `AppCloseBehavior`
- политики в `Torrents/Policies/*`

Но перед чтением важно понимать:
- это **не текущий основной runtime path**;
- это seed следующего архитектурного этапа;
- рабочий путь по-прежнему snapshot/service-centered.

Именно поэтому не надо начинать чтение проекта с `TorrentEntry` и думать, что весь код уже построен вокруг него.

---

## 5. Куда новичку не лезть первым PR
Не начинать с:
- `MonoTorrentService.cs`
- `TorrentUpdatePublisher.cs`
- `TorrentStartupCoordinator.cs`
- `App.xaml.cs`
- `AppCloseCoordinator.cs`

Сначала безопаснее:
- читать use case'ы;
- смотреть ViewModel и workflow flow;
- разбираться в docs;
- потом уже заходить в инфраструктурный runtime path.

---

## 6. Что считать активными документами
Активные:
- `CURRENT_ARCHITECTURE_STATE.md`
- `README_ARCHITECTURE.md`
- `PROJECT_MAP.md`
- `TRANSITION_BACKLOG.md`
- `ANTI_PATTERNS.md`
- `TECH_DEBT_BACKLOG.md`
- `REGRESSION_CHECKLIST.md`

Плановые/целевые:
- `TARGET_ARCHITECTURE.md`
- `TARGET_ARCHITECTURE_V2.md`
- `ARCHITECTURE_TRANSITION_PLAN.md`
- `REFORM_PLAN.md`

Исторические checkpoint-документы нужно читать только после понимания текущего состояния.
