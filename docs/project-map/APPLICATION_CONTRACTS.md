# Application Contracts

Status: active
Last updated: 2026-04-06
Source references: `TARGET_ARCHITECTURE.md`, `USER_APP_LOGIC.md`

## 1. Purpose

This document defines the application-layer contracts that presentation and infrastructure rely on.

## 2. Contract Design Rules

1. Contracts are scenario-oriented, not engine-oriented.
2. Contracts must preserve user intent semantics.
3. Contracts must support deferred execution where runtime readiness is not guaranteed.
4. Contracts must expose stable outcomes to callers.

## 3. Read Contracts

## 3.1 `ITorrentReadModelFeed`

Responsibility:
- provide projected torrent list for UI rendering;
- push updates when projection changes.

Guarantees:
- initial snapshot is available without waiting for full engine readiness;
- projection is stable against transient runtime failures;
- no duplicate torrent identities in one snapshot.
- startup snapshot is sourced from app-owned persisted collection, not from engine-wide restore artifacts.

## 4. Write and Command Contracts

## 4.1 `ITorrentWriteService`

Responsibility:
- execute canonical add operation after preparation/preview confirmation.

Guarantees:
- add operation is atomic at application meaning level (either accepted and persisted, or rejected);
- duplicate policy is enforced before creating a new entry.
- add flow persists per-torrent source material required for future staged rehydration.

## 4.2 `ITorrentCommandService` (or equivalent command boundary)

Responsibility:
- process `Start`, `Pause`, `Remove` commands.

Guarantees:
- command acceptance persists user intent immediately;
- runtime apply may be immediate or deferred;
- result communicates `Success`, `Deferred`, or `NotFound` semantics clearly.

## 5. Engine and Runtime Contracts

## 5.1 `ITorrentEngineLifecycle`

Responsibility:
- startup/shutdown lifecycle of engine integration.

Guarantees:
- startup and shutdown are explicit operations;
- startup initializes an empty engine instance and excludes `ClientEngine.RestoreStateAsync` from the canonical path;
- lifecycle failure does not directly force UI crash.

## 5.2 `ITorrentEngineStatusService`

Responsibility:
- expose readiness and readiness change notifications.

Guarantees:
- readiness transitions are observable for deferred action replay/sync workflows.

## 5.3 `ITorrentEngineMaintenanceService`

Responsibility:
- persistence and maintenance operations tied to startup/shutdown flow.

Guarantees:
- maintenance operations are safe to call in shutdown path;
- failure is recoverable at next startup.

## 6. Collection and Runtime Facts Contracts

## 6.1 Collection repository contract

Responsibility:
- own persisted torrent collection (identity, user intent, settings, deferred actions).

Guarantees:
- durable save boundary;
- consistent read after successful save;
- remove semantics align with selected remove mode.

## 6.2 Runtime facts provider contract

Responsibility:
- provide runtime observations for synchronization/projection.

Guarantees:
- runtime facts can be unavailable without breaking app-level behavior;
- sync workflows consume facts best-effort.

## 7. Source Preparation and Preview Contracts

## 7.1 Preparation contract

Responsibility:
- convert `.torrent` and magnet inputs into one prepared torrent representation.

Guarantees:
- magnet and `.torrent` share one normalized output model after metadata availability;
- output is suitable for duplicate checks and preview flow.

## 7.2 Preview dialog contract

Responsibility:
- collect user confirmation, destination, and file subset selection.

Guarantees:
- cancellation is explicit and side-effect-free;
- confirmed result is deterministic input to add use case.

## 8. Shell Contracts

## 8.1 Window close workflow contract

Responsibility:
- resolve close behavior based on settings and close source.

Guarantees:
- main window close respects close setting;
- result is explicit action (`MinimizeToTray` or `ExitApplication`).

## 8.2 Tray exit workflow contract

Responsibility:
- resolve explicit tray exit action.

Guarantees:
- always resolves to full app exit.

## 9. Settings Contracts

Responsibility:
- support read/edit/save/apply lifecycle.

Guarantees:
- unchanged values are preserved unless explicitly modified;
- save failure does not silently partially apply settings;
- settings behavior is consistent across pages.

## 10. Cross-Contract Invariants

1. User intent is never discarded because runtime is unavailable.
2. Runtime refresh never silently invalidates persisted user intent.
3. One torrent identity maps to one UI list item.
4. Remove mode is always user-chosen; shortcut flows still require choice dialog.
5. Magnet handling is UI/internal-scenario based; system `magnet:` protocol activation is out of scope unless explicitly approved.

## 11. Change Policy

When changing a contract:
1. update this file with new guarantee/semantics;
2. update `TARGET_ARCHITECTURE.md` if boundary semantics change;
3. update `USER_APP_LOGIC.md` if user-visible behavior changes;
4. add or update ADR when decision is architectural and non-trivial.

## 12. 2026-04-10 Contract Update

### 12.1 Domain safety baseline

- Domain entities/value objects must reject invalid state at creation time.
- Collections crossing domain boundaries must be copied defensively.

### 12.2 Add flow result contract

- `AddTorrentUseCase` returns an explicit result model:
  - `AddTorrentOutcome.Success`
  - `AddTorrentOutcome.AlreadyExists`
  - `AddTorrentOutcome.InvalidInput`
  - `AddTorrentOutcome.StorageError`
- Add flow must not leave partial persisted state on storage failure; compensation is required.

### 12.3 Command orchestration contract

- User command execution (`Start`, `Pause`, `Remove`) is orchestrated via a unified command use case (`TorrentCommandUseCase`) with typed command input (`TorrentCommandType`).
- Command result semantics remain based on `Success` / `Deferred` / `NotFound` at command-service boundary.

### 12.4 Collection repository save boundary

- `ITorrentCollectionRepository.SaveAsync` does not expose flush-policy flags (for example `force`).
- Durability/flush strategy is an infrastructure concern.

### 12.5 UI boundary rule (enforced)

- Application layer must not own dialog contracts.
- UI dialog contract (`IDialogService`) belongs to presentation-facing abstractions.

### 12.6 Workflow dependency rule

- Infrastructure orchestration components should consume application workflow interfaces, not concrete workflow classes, to keep coupling directional and replaceable.
