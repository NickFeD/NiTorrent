# NiTorrent — ANTI_PATTERNS

## Назначение
Этот файл фиксирует ошибки, которые **уже видны в текущем коде** и которые нельзя усиливать дальше.

Документ привязан к реальному состоянию проекта, а не к идеальной целевой архитектуре.

---

## 1. `MonoTorrentService` как god facade
### Как выглядит сейчас
`src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs` одновременно:
- инициализирует engine;
- обслуживает preview/add/start/pause/stop/remove;
- управляет сохранением и shutdown;
- является boundary для UI/application;
- координирует фоновые обновления.

### Почему это плохо
- высокая связанность;
- трудно тестировать по частям;
- любой новый сценарий естественно тянется добавляться именно туда.

### Что не делать
- не расширять `MonoTorrentService` новыми продуктово-значимыми обязанностями;
- не превращать его в место для новых правил duplicate/restore/intent/close behavior.

---

## 2. Смешение polling, events и catalog-sync как единой update-модели
### Как выглядит сейчас
Текущий список торрентов обновляется через комбинацию:
- `TorrentMonitor` (polling каждые 2 секунды)
- `PublishTorrentUpdates()`
- `TorrentEventOrchestrator`
- `TorrentUpdatePublisher`
- `TorrentCatalogSnapshotSynchronizer`
- подписку `TorrentViewModel` на `ITorrentService.UpdateTorrent`

### Почему это плохо
- сложнее понимать источник истины;
- выше риск регрессий с дублями и рассинхронизацией cached/live состояния;
- сложно безопасно менять timing startup и restore.

### Что не делать
- не добавлять ещё один путь обновления списка рядом с этим;
- не плодить новые side effects внутри publish/update методов.

---

## 3. Product-centered domain model лежит рядом, но не управляет системой
### Как выглядит сейчас
В `NiTorrent.Domain` уже есть:
- `TorrentEntry`
- `TorrentIntent`
- `DeferredAction`
- `AppCloseBehavior`
- политики duplicate/status/deferred

Но рабочий код всё ещё центрируется вокруг:
- `ITorrentService`
- `TorrentSnapshot`
- snapshot-based catalog/runtime sync.

### Почему это плохо
- архитектура начинает раздваиваться;
- новичку трудно понять, что реально активно;
- легко начать писать код сразу в обе модели.

### Что не делать
- не использовать `TorrentEntry` как будто он уже главный источник истины;
- не писать docs так, будто переход уже завершён;
- не добавлять новый рабочий path параллельно старому без явного удаления старого.

---

## 4. Толстая `TorrentViewModel`
### Как выглядит сейчас
`src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`:
- подписывается на runtime updates;
- синхронизирует список item view model;
- держит aggregate speeds;
- вызывает workflow-команды;
- показывает ошибки через dialog service.

### Почему это плохо
Даже после появления workflow-слоя это всё ещё главный presentation hotspot.

### Что не делать
- не добавлять сюда engine/recovery/storage логику;
- не тащить сюда ещё один способ чтения списка;
- не помещать сюда правила duplicate/restore/intent.

---

## 5. Неявные side effects в settings flow
### Как выглядит сейчас
`TorrentSettingsViewModel.Save()`:
- по одному пишет свойства в `ITorrentPreferences`;
- затем отдельно вызывает `ApplyTorrentSettingsUseCase`.

### Почему это плохо
- persistence и runtime-apply происходят как два разных шага;
- частичная ошибка после сохранения может оставить уже записанный config;
- staged-edit модель формы существует, но backend сохраняет свойства по одному.

### Что не делать
- не усиливать этот pattern новыми похожими настройками без явного описания границ `save` vs `apply`.

---

## 6. Hidden temporal coupling на startup и restore
### Как выглядит сейчас
Система корректно работает только если соблюдается порядок:
- host стартует;
- cached snapshots публикуются;
- engine поднимается;
- monitor начинает тикать;
- UI уже подписан на updates.

### Почему это плохо
- сложно безопасно переносить код;
- баги часто проявляются только на cold start или после interrupted shutdown.

### Что не делать
- не менять startup path без прохождения расширенного regression-checklist;
- не добавлять новые фоновые задачи, которые публикуют состояние торрентов без явной схемы порядка.

---

## 7. Документация “цель” начинает маскироваться под “текущее состояние”
### Как выглядит сейчас
В проекте есть одновременно:
- активные docs о текущем состоянии;
- transition docs;
- target docs.

### Почему это плохо
Если не различать их явно, разработчик начинает верить, что `TorrentEntry` и `AppCloseBehavior` уже управляют рабочим кодом, хотя это не так.

### Что делать
- для текущего состояния использовать `CURRENT_ARCHITECTURE_STATE.md`;
- для цели — `TARGET_ARCHITECTURE_V2.md`;
- при расхождении не переписывать реальность под документы.
