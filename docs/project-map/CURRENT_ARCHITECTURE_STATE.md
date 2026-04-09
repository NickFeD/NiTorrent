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
- UI list/read-side теперь строится через `GetTorrentListQuery` и `TorrentListItemReadModel`, а не через прямую публикацию runtime snapshot'ов в Presentation.
- Details screen переведён на отдельный `GetTorrentDetailsQuery` + `UpdatePerTorrentSettingsWorkflow`.
- Close-flow проходит через application workflows `HandleWindowCloseWorkflow` и `HandleTrayExitWorkflow`, а `AppCloseCoordinator` стал thin shell adapter.

- Periodic runtime reconciliation проходит через `SyncTorrentCollectionFromRuntimeWorkflow`, а не через snapshot-publisher внутри infrastructure.

## Post-Remediation Verification (2026-04-05)

This section records the current acceptance verification against
`ARCH_REMEDIATION_PLAN.md` after implementing C1-C4, M1-M8, and m1.

### Critical Status

| Critical | Status | Evidence |
|---|---|---|
| C1: deferred replay after readiness/resync | Closed | Replay workflow and trigger wiring in restore + runtime invalidation paths; deferred entries removed only on successful apply |
| C2: intent-first runtime synchronization | Closed | `TorrentCollectionRestorePolicy` applies runtime facts without overriding persisted paused intent |
| C3: user-facing open folder error flow | Closed | `OpenFolderAsync` wrapped in `try/catch` with `UserErrorMapper` and `ShowTextAsync` |

### USER_APP_LOGIC Section 3 Matrix (Post-Remediation)

| Scenario | Status | Verification Source |
|---|---|---|
| add `.torrent` via unified flow | Implemented | Code path unchanged; covered by existing flow boundaries |
| add magnet via unified flow | Implemented | Code path unchanged; covered by existing flow boundaries |
| duplicate handling | Implemented | Code path unchanged; protected in preview/add flow |
| remove mode choice | Implemented | UI + workflow behavior preserved |
| open folder | Implemented | C3 error handling unified with other commands |
| close/tray/exit | Implemented | M4/M5 async close flow + AskUser mapping in app coordinator |

### Verification Classification

#### Confirmed by tests
- `NiTorrent.Domain.Tests`: 2 passed (`dotnet vstest ...NiTorrent.Domain.Tests.dll`)
- `NiTorrent.Application.Tests`: 3 passed (`dotnet vstest ...NiTorrent.Application.Tests.dll`)
- Additional acceptance-focused tests were added in source:
  - `PriorityAcceptanceVerificationTests.CommandsBeforeEngineReadiness_StartIsDeferred_AndStored`
  - `PriorityAcceptanceVerificationTests.StartupRestore_RunningIntent_ReplaysDeferredStart`
  - `PriorityAcceptanceVerificationTests.StateMappingAfterRestart_PausedIntent_RemainsPaused_AfterRuntimeSync`
- In this local environment they are not yet executed because test-project rebuild currently fails at MSBuild workload resolution.
- Covered invariants:
  - intent-first mapping for paused intent (C2 invariant)
  - replay coordinator gating, deduplication, and cycle outcome logging (C4)

#### Confirmed by code/build inspection
- Domain/Application build succeeds in current workspace:
  - `dotnet build src/NiTorrent.Domain/NiTorrent.Domain.csproj --no-restore`
  - `dotnet build src/NiTorrent.Application/NiTorrent.Application.csproj --no-restore`
- Code-level verification confirms:
  - deferred action persistence/removal semantics for C1
  - open folder user-safe error reporting for C3
  - runtime facts are sanitized before user-facing rendering (M2)
  - details status uses user-facing mapper instead of raw enum (M7)
  - AskUser close behavior no longer silently falls back to immediate exit (M5)

#### Manual/scenario verification still required
- Commands before engine readiness (`Start/Pause/Remove`) in full UI lifecycle.
- Startup/restore list behavior with runtime resync under real engine timing.
- End-to-end state mapping perception in UI (paused/stopped + runtime transitions).
- Error UX scenarios in real shell environment (folder missing, runtime fault dialogs).
- Close/tray/exit interactions (`X`, tray close, tray exit) in UI runtime.

### Build Environment Note

In this shell session, `dotnet build` for WinUI-facing projects (`NiTorrent.App`,
`NiTorrent.Presentation`, `NiTorrent.Infrastructure`) reports restore-stage failure
without compile diagnostics (workload resolver/MSBuild restore-path issue in environment).
This does not invalidate completed unit-level verification, but app-level acceptance
for UI scenarios remains manual in this environment.

## Startup Restore Redesign Update (2026-04-06)

- Startup engine bootstrap moved to empty-engine creation path.
- `ClientEngine.RestoreStateAsync` is no longer part of canonical startup restore flow.
- Application now persists per-torrent source bytes for `.torrent` and magnet-derived adds.
- Restore workflow executes staged per-torrent rehydration with running-intent priority.
- Legacy catalog entries without persisted source are retained and marked with recoverable runtime error state instead of being dropped.

## Torrent Details Update (2026-04-09)

- `TorrentDetailsPage` now contains:
  - header/action block with status, `dev:StorageBar`, progress, transfer rates, ETA, ratio, save path, hash, and start/pause/open-folder/delete actions;
  - live transfer chart (download + upload on one graph);
  - extended details (general, transfer, technical, connections, tracker summary);
  - inline peers list;
  - inline trackers list;
  - per-torrent settings block (existing save flow preserved).

- Live chart behavior:
  - chart history is in-memory only (`TorrentDetailsViewModel.SpeedHistory`);
  - history is capped to a fixed point count (`MaxChartPoints`) to avoid UI overload;
  - history is not persisted and starts empty after application restart.

- Peers behavior:
  - peers are polled only while `TorrentDetailsPage` is active (`Activate`/`Deactivate`);
  - polling is stopped on page navigation away;
  - peers are updated via keyed diff (`ObservableCollection` + dictionary), without full list recreation each refresh.

- Architecture changes:
  - added application boundary `ITorrentDetailsRuntimeService`;
  - added query `GetTorrentRuntimeDetailsQuery`;
  - added infrastructure adapter `EngineBackedTorrentDetailsRuntimeService` (runtime snapshot mapping for details/peers/trackers);
  - `TorrentDetailsViewModel` now consumes dedicated details query + runtime query + workflow commands, keeping UI and business boundaries separated.
