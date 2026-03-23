# NiTorrent — REFORM_STATUS_REPORT

## Что это за checkpoint
Этот отчёт фиксирует **реальное состояние кода в архиве**, а не целевую архитектуру.

Точка остановки сейчас такая:
- проект уже не монолит уровня `App.xaml.cs + raw UI calls`;
- activation и часть сценариев вынесены в application/workflow слой;
- но главный runtime path всё ещё центрируется вокруг `ITorrentService` и `MonoTorrentService`.

---

## Что реально сделано

### 1. UI больше не координирует сценарии полностью самостоятельно
Сейчас:
- `TorrentViewModel` использует `ITorrentWorkflowService` для add/start/pause/remove/open-folder;
- `AppActivationService` использует workflow-level file activation flow;
- `TorrentPreviewFlow` выделяет preview + confirm + add orchestration.

Это уже лучше, чем прямые orchestration-вызовы из `App.xaml.cs` и ViewModel.

### 2. Close-flow и startup стали чище
Сейчас есть отдельные сервисы:
- `AppStartupService`
- `AppActivationService`
- `AppCloseCoordinator`
- `AppShutdownCoordinator`
- `IMainWindowLifecycle`

Но close behavior всё ещё читается через `MinimizeToTrayOnClose`, а не через новый `AppCloseBehavior`.

### 3. Domain-first модель уже присутствует, но не управляет рабочим кодом
В домене добавлены:
- `TorrentEntry`
- `TorrentIntent`
- `TorrentKey`
- `TorrentRuntimeState`
- `DeferredAction`
- политики duplicate/status/deferred

Но они пока почти не встроены в рабочий runtime path.

---

## Что остаётся главным ограничением

### Главный активный runtime boundary
- `ITorrentService`
- `MonoTorrentService`

### Главный активный read-side
- `TorrentSnapshot`
- `ITorrentService.UpdateTorrent`
- `TorrentUpdatePublisher`
- `TorrentCatalogSnapshotSynchronizer`
- `TorrentMonitor`

### Что это означает
Код уже стал слоистее, но целевая product-centered архитектура из `TARGET_ARCHITECTURE_V2.md` пока не достигнута.

---

## Честная оценка состояния

### Сильные стороны
- слои уже разделены по проектам;
- activation вынесен из entry point;
- UI работает через use case/workflow слой;
- startup и shutdown стали читаемее;
- есть seed следующей архитектуры в `Domain`.

### Ограничения
- `MonoTorrentService` остаётся слишком центральным;
- snapshot pipeline остаётся главным read-side;
- polling + publish + catalog sync образуют смешанную update-модель;
- целевая доменная модель лежит рядом, но ещё не является источником истины.

---

## Что считать следующими большими шагами
1. перестать расширять `MonoTorrentService` как главный фасад;
2. вытащить product decisions из snapshot/update pipeline;
3. перевести read-side от snapshot-потока к product read model;
4. заменить bool-настройки close behavior на продуктовую модель;
5. синхронизировать target docs с реальными шагами миграции.
