# NFR and SLO

Status: active
Last updated: 2026-04-05
Source references: `USER_APP_LOGIC.md`, `TARGET_ARCHITECTURE.md`

## 1. Purpose

This document defines non-functional requirements and service-level objectives that make user behavior predictable and architecture enforceable.

## 2. Responsiveness Objectives

1. Startup perceived responsiveness
- The main window should become interactive quickly.
- Torrent list should appear from persisted catalog before full engine readiness.
- Engine initialization must run in background without blocking initial UI interaction.

2. Settings page responsiveness
- Opening settings pages must not block on engine startup.
- Read/edit/save/apply model must keep local editing responsive.

3. Runtime update responsiveness
- Frequent torrent status updates must not freeze list interactions.
- Selection and command availability should remain stable during projection refreshes.

4. Tray responsiveness
- Tray tooltip/menu updates must be best-effort and non-blocking.
- Tray updates must not degrade main UI responsiveness.

## 3. Reliability Objectives

1. Intent durability
- Start/Pause/Remove commands must be accepted and persisted even before engine readiness.
- Deferred actions must be applied after engine readiness without user re-entry.

2. Catalog durability
- Torrent list identity and user intent must survive restart.
- Removed torrents must not reappear after successful remove persistence.

3. Safe degradation
- Corrupted runtime state must not break app startup.
- App may degrade to catalog-only view until runtime sync stabilizes.

## 4. Consistency Objectives

1. Unified add behavior
- `.torrent` and magnet must converge to one prepared representation as early as reasonably possible.
- Duplicate checks and preview/validation must run in one canonical path.

2. Unified paused/stopped presentation
- Internal `Paused` and `Stopped` may differ.
- UI must expose them as one user-facing state (`paused/stopped`).

3. Close/exit consistency
- Main window close action follows the close setting.
- Tray `Exit` always forces full exit and ignores close setting.

4. Remove mode consistency
- Remove action must present user choice between:
  - remove record only;
  - remove record + downloaded files.
- Future quick-remove/hotkey flows must still show the same choice dialog.

## 5. Error Experience Objectives

1. User-facing messages
- Messages must be localized and actionable.
- Raw engine/internal exceptions must not be shown directly to users.

2. Failure containment
- A failure in one background workflow should not crash unrelated UI flows.
- Projection refresh should continue best-effort after recoverable failures.

## 6. Validation Strategy

1. Automated checks
- Domain tests: duplicate policy, deferred policy, state mapping invariants.
- Application tests: restore flow, deferred command replay, add-flow unification.
- Integration tests: catalog/runtime synchronization and shutdown persistence.

2. Regression checklist alignment
- Each change touching these NFR/SLO areas should update `REGRESSION_CHECKLIST.md` when required.

## 7. Change Control

Any change that weakens an objective in this file requires:
1. explicit architecture justification;
2. update to `TARGET_ARCHITECTURE.md` and/or `USER_APP_LOGIC.md`;
3. rollback/mitigation plan if objective is temporarily reduced.
