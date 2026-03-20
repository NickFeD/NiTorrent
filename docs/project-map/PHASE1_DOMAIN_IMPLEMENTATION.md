# NiTorrent — PHASE 1 DOMAIN IMPLEMENTATION

## Что сделано на Этапе 1
В проект добавлен первый набор новой доменной модели из `ARCHITECTURE_TRANSITION_PLAN.md`.

Новые элементы в `NiTorrent.Domain`:
- `TorrentKey`
- `TorrentIntent`
- `TorrentLifecycleState`
- `TorrentRuntimeState`
- `DeferredAction`
- `DeferredActionType`
- `TorrentEntry`
- `AppCloseBehavior`
- `GlobalTorrentSettings`
- `TorrentDuplicatePolicy`
- `TorrentStatusResolver`
- `DeferredActionPolicy`
- `TorrentLifecycleStateMapper`

---

## Что это значит
Теперь в проекте появилось новое ядро, которое может описывать торрент как продуктовую сущность без привязки к MonoTorrent и без опоры на один только `ShouldRun`.

Новая модель уже умеет выражать:
- пользовательское намерение (`Run` / `Pause`)
- runtime facts отдельно от intent
- эффективный статус на основе intent + runtime
- отложенные действия до готовности движка
- product-level close behavior
- product-level global torrent settings

---

## Что пока остаётся legacy
На этом этапе ещё НЕ перенесены:
- current catalog format
- `MonoTorrentService`
- snapshot-based restore flow
- bool `ShouldRun`
- current settings repository contracts

Это ожидаемо. Этап 1 создаёт новую модель, но ещё не делает её единственным источником истины.

---

## Чего нельзя делать дальше
Нельзя:
- продолжать складывать новую бизнес-логику в `Infrastructure`;
- использовать `TorrentSnapshot` как замену `TorrentEntry` в новой архитектуре;
- создавать новый большой facade, который снова станет скрытым центром системы;
- дублировать правила intent/restore/deferred actions в нескольких слоях.

---

## Что должно быть следующим шагом
Следующий архитектурный шаг после этого этапа:
- ввести `ITorrentCollectionRepository`
- начать хранить продуктовую коллекцию как `TorrentEntry`
- построить мост `legacy catalog -> product repository`
