# Regression Checklist

## После изменений в торрент-сценариях проверить
- `.torrent` файл открывается через preview и не создаёт дубль;
- magnet проходит через тот же preview-flow;
- file association использует тот же add-flow;
- start / pause / remove работают из UI и после перезапуска сохраняют ожидаемое пользовательское намерение;
- явный `Выход` из трея всегда закрывает приложение полностью;
- настройки имеют draft -> apply модель и после ошибки применения откатываются;
- пользователь не видит сырые исключения MonoTorrent или инфраструктуры.

## После изменений в storage/settings проверить
- `TorrentConfig`, `AppConfig` и `TorrentEntrySettingsConfig` сохраняются и читаются корректно;
- каталог торрентов не ломается при повторном старте;
- duplicate check не даёт создать второй элемент для того же торрента.

## Архитектурная проверка
- не появился второй параллельный command/write path;
- Presentation не зависит от MonoTorrent;
- Infrastructure не диктует пользователю свои внутренние состояния и ошибки.

## Final Acceptance Verification (2026-04-05)

This block mirrors mandatory checks from `ARCH_REMEDIATION_PLAN.md`.

### 1) Commands before engine readiness
- Automated:
  - Deferred replay coordination and deterministic replay cycle behavior are covered by `NiTorrent.Application.Tests` (3 tests passed via `dotnet vstest`).
  - Additional minimal test was added: `PriorityAcceptanceVerificationTests.CommandsBeforeEngineReadiness_StartIsDeferred_AndStored`.
  - Execution status for the new test: pending in this local environment due MSBuild workload resolver failure when rebuilding test assembly.
- Code/build evidence:
  - Deferred persistence and apply/remove semantics are implemented in command/deferred workflows.
- Manual scenario required:
  - UI-driven `Start/Pause/Remove` before engine ready and replay after readiness/runtime invalidation.
  - Procedure:
    1. Start app with engine intentionally unavailable/unready.
    2. Trigger `Start`, `Pause`, `Remove` for different entries.
    3. Verify commands return deferred outcome and entries persist deferred actions after restart.
    4. Bring engine to ready state or force runtime invalidation event.
    5. Verify deferred actions replay automatically without manual retry.

### 2) Startup/restore
- Automated:
  - Additional minimal test was added: `PriorityAcceptanceVerificationTests.StartupRestore_RunningIntent_ReplaysDeferredStart`.
  - Execution status for the new test: pending in this local environment due MSBuild workload resolver failure when rebuilding test assembly.
- Code/build evidence:
  - Restore workflow keeps catalog-first read model and applies replay gate.
- Manual scenario required:
  - verify no duplicates/phantom items and no lost intent after app restart with real runtime.
  - Procedure:
    1. Prepare catalog with at least 3 torrents and mixed intents (running/paused).
    2. Restart app while runtime is initializing.
    3. Confirm early list shows persisted entries before full engine sync.
    4. After sync, confirm no duplicate/phantom items and no intent loss.

### 3) State mapping
- Automated:
  - `NiTorrent.Domain.Tests` (2 tests passed via `dotnet vstest`) confirm paused intent is not elevated by runtime facts.
  - Additional minimal test was added: `PriorityAcceptanceVerificationTests.StateMappingAfterRestart_PausedIntent_RemainsPaused_AfterRuntimeSync`.
  - Execution status for the new test: pending in this local environment due MSBuild workload resolver failure when rebuilding test assembly.
- Code/build evidence:
  - intent-first guard applied in restore policy.
- Manual scenario required:
  - visual confirmation of paused/stopped semantics under live runtime transitions.
  - Procedure:
    1. Set torrent intent to paused and restart app.
    2. Ensure runtime reports active state (downloading/seeding) for same torrent.
    3. Verify UI remains in paused/stopped semantics and does not elevate to active state.
    4. Confirm start action is still available and pause action remains disabled as expected.

### 4) Error UX
- Automated:
  - no UI automation for dialogs in current change set.
- Code/build evidence:
  - open folder command catches exceptions and routes to `UserErrorMapper` + dialog text output.
  - raw runtime exception text is not used as user-facing error text.
- Manual scenario required:
  - open folder failure path and runtime-error rendering validation in real UI shell.
  - Procedure:
    1. Select torrent with invalid or removed save path.
    2. Trigger `Open Folder`.
    3. Verify app does not crash and shows mapped user-facing error dialog.
    4. Verify dialog does not include raw exception/stack/MonoTorrent internals.

### 5) Close/tray/exit
- Automated:
  - no end-to-end UI automation in current change set.
- Code/build evidence:
  - async close workflow/query and AskUser mapping to explicit user decision path are implemented.
- Manual scenario required:
  - `X` close behavior, minimize-to-tray, and tray exit in desktop UI lifecycle.
  - Procedure:
    1. Configure close behavior to each mode (`MinimizeToTray`, `ExitApplication`, `AskUser`).
    2. Press window `X` and verify behavior exactly matches selected mode.
    3. For `AskUser`, verify explicit choice is shown and respected.
    4. Trigger tray `Exit` and verify full process termination (no tray ghost process).

## Acceptance Snapshot

- Critical items (C1, C2, C3): closed by code + targeted tests.
- Backlog acceptance items (C4, M1-M8, m1): implemented; part covered by tests, part by code review, remaining UI-heavy checks are manual.
- Documentation: updated in same change set (`CURRENT_ARCHITECTURE_STATE.md`, `REGRESSION_CHECKLIST.md`).

## Startup Restore Redesign Checks (2026-04-06)

- startup does not call `ClientEngine.RestoreStateAsync` in the canonical path;
- engine starts as empty runtime and receives settings explicitly;
- UI list appears from catalog before staged runtime rehydration completes;
- torrents with `Running` intent are rehydrated before lower-priority entries;
- missing persisted source does not crash startup and does not silently remove catalog item;
- add `.torrent` and magnet persist source bytes needed for future rehydration.
