# Architecture README

Читайте документы в таком порядке:
1. `USER_APP_LOGIC.md` — продуктовые правила и поведение.
2. `CURRENT_ARCHITECTURE_STATE.md` — текущее живое устройство проекта.
3. `ANTI_PATTERNS.md` — чего нельзя делать в дальнейшем рефакторинге.
4. `REGRESSION_CHECKLIST.md` — что проверять после изменений.

## Коротко о текущей архитектуре
- UI работает через `ITorrentWorkflowService`.
- Start/Pause/Remove выполняются через `ITorrentCommandService` и intent/deferred policies.
- Add/Preview/ApplySettings выполняются через `ITorrentWriteService`.
- Startup restore идёт через `IRestoreTorrentCollectionWorkflow`.
- Read-side приходит через `ITorrentReadModelFeed`, который проецирует `TorrentEntry` + runtime facts в UI read models.
- MonoTorrent и файловое хранение остаются внутри Infrastructure.
- Настройки хранятся через `nucs.JsonSettings`-based config classes.

## Какая command-архитектура считается правильной
Правильная command-архитектура проекта — это `ITorrentCommandService` + restore/deferred workflows.
Историческая infrastructure-очередь команд (`TorrentCommandQueue` / queued intent inside startup) удалена и не должна возвращаться.


## 2026-03 alignment update
- `ITorrentCommandService` остаётся единственным центром пользовательских команд.
- `ITorrentWriteService` ограничен сценариями add/preview/apply-settings.
- UI list/read-side теперь строится через `GetTorrentListQuery` и `TorrentListItemReadModel`, а не через прямую публикацию runtime snapshot'ов в Presentation.
- Details screen переведён на отдельный `GetTorrentDetailsQuery` + `UpdatePerTorrentSettingsWorkflow`.
- Close-flow проходит через application workflows `HandleWindowCloseWorkflow` и `HandleTrayExitWorkflow`, а `AppCloseCoordinator` стал thin shell adapter.

- Periodic runtime reconciliation проходит через `SyncTorrentCollectionFromRuntimeWorkflow`, а не через snapshot-publisher внутри infrastructure.
