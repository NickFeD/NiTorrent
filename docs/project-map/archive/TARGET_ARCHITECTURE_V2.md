# Target architecture v2

Статус: **active target and current implementation baseline**.

## Принцип

Архитектура строится вокруг продуктовых сценариев и стабильных application boundaries, а не вокруг legacy service facade.

## Слои

### Domain
Хранит `TorrentEntry`, `TorrentRuntimeState`, `DeferredAction`, policy-классы и projection policy. Domain не знает о MonoTorrent и UI.

### Application
Хранит use cases, workflows и контракты:
- read: `ITorrentReadModelFeed`
- write: `ITorrentWriteService`
- engine lifecycle/status/maintenance
- collection/runtime facts/engine gateway ports

### Infrastructure
Реализует application ports поверх MonoTorrent runtime, catalog store и background orchestration.

### Presentation / App
Используют только application contracts. UI не зависит от engine internals и не знает про legacy adapters.

## Правила
1. Новые UI сценарии не должны ссылаться на infrastructure classes напрямую.
2. Новые workflows не должны добавлять compatibility facade.
3. Один контракт — одна активная реализация линии. Временные дубликаты запрещены.
4. Все migration-only adapters должны удаляться сразу после замены.
5. Статус архитектуры фиксируется в `CURRENT_ARCHITECTURE_STATE.md`, а не в фазовых заметках.
