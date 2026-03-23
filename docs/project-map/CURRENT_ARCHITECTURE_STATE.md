# Current Architecture State

Проект работает на новой слоистой архитектуре без legacy facade `ITorrentService`.
Живой command-path для пользовательских команд проходит через `ITorrentWorkflowService` -> use cases -> `ITorrentCommandService`.
`ITorrentWriteService` остаётся write-boundary только для add/preview/apply-settings.
Старая infrastructure-очередь пользовательских команд удалена: intent и deferred actions теперь живут только в domain/application модели.

## Активные границы
- `ITorrentWorkflowService` — UI-facing orchestration для действий пользователя.
- `ITorrentCommandService` — application command boundary для start/pause/remove с intent/deferred semantics.
- `IRestoreTorrentCollectionWorkflow` — startup restore path, который синхронизирует persisted collection с runtime и применяет deferred actions.
- `ITorrentWriteService` — application write boundary для preview/add/apply-settings.
- `ITorrentReadModelFeed` — read-side проекция UI read models из product-owned collection + runtime facts.
- `ITorrentEngineStatusService` / `ITorrentEngineMaintenanceService` — shell/runtime integration.
- `ITorrentCollectionRepository` — product-level доступ к persisted torrent catalog; используется для duplicate checks, restore и работы с пользовательской коллекцией.
- `ITorrentSettingsRepository` / `ITorrentEntrySettingsRepository` — settings storage.

## Что удалено
- `ITorrentService` и зависимость верхних слоёв от него.
- Параллельный runtime command flow с `TorrentCommandQueue`, который дублировал intent/deferred semantics.
- Direct command methods на `ITorrentWriteService`.

## Persisted storage
- `TorrentCatalogStore` хранит пользовательскую коллекцию и ранний cached state для старта приложения.
- `TorrentConfig` и `AppConfig` используют `nucs.JsonSettings`.
- `TorrentEntrySettingsConfig` использует `nucs.JsonSettings` для per-torrent settings.

## Принятые продуктовые решения
- magnet-ссылка идёт через тот же preview-flow, что и `.torrent`; metadata подгружаются до preview;
- при битом runtime state приложение не обязано удерживать в UI несуществующие runtime-торренты;
- для пользователя `Paused` и `Stopped` могут отображаться одинаково;
- отдельное пользовательское состояние `Завершён` не планируется.


## 2026-03 alignment update
- `ITorrentCommandService` остаётся единственным центром пользовательских команд.
- `ITorrentWriteService` ограничен сценариями add/preview/apply-settings.
- UI list/read-side теперь строится через `GetTorrentListQuery` и `TorrentListItemReadModel`, а не через прямую публикацию snapshot'ов в Presentation.
- Details screen переведён на отдельный `GetTorrentDetailsQuery` + `UpdatePerTorrentSettingsWorkflow`.
- Close-flow проходит через application workflows `HandleWindowCloseWorkflow` и `HandleTrayExitWorkflow`, а `AppCloseCoordinator` стал thin shell adapter.

- Runtime invalidation больше не публикует UI-facing snapshot payloads: инфраструктура только сигналит об изменении runtime facts и инвалидирует read-side projection.
- `TorrentSnapshot` и `TorrentSnapshotFactory` удалены из активного графа; read-side и persisted collection больше не опираются на snapshot-centric contracts.
