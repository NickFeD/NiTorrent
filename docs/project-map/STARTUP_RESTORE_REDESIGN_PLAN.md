# Startup/Restore Redesign Plan

Status: active  
Last updated: 2026-04-06

## 1. Current State Summary

- Startup previously depended on engine-wide restore from `torrent_engine.dat` via `ClientEngine.RestoreStateAsync`.
- Catalog persistence already existed for torrent identity, intent, deferred actions, and cached UI status.
- Fast UI restore was possible, but runtime restore ownership remained engine-centric.

## 2. Target Architecture

- Startup source of truth is **app-owned persistence only**.
- `ClientEngine.RestoreStateAsync` is **excluded** from target startup path.
- Engine starts as an empty `ClientEngine` with settings applied explicitly.
- Rehydration order and scope are controlled by app workflows, not by engine-wide state load.

## 3. Persistence Model

- `torrent_catalog.json` persists:
  - identity and metadata;
  - save path;
  - selected files;
  - user intent;
  - deferred actions;
  - per-torrent settings;
  - cached startup projection.
- `Torrents/sources/` persists per-torrent source bytes (`.torrent` copy or magnet-materialized torrent bytes).
- `torrent_engine.dat` may remain as secondary artifact, but must not control startup restore.

## 4. Startup/Rehydration Flow

1. Load settings + catalog and publish fast UI projection immediately.
2. Initialize **empty** engine instance.
3. Build staged rehydration queue from catalog (`Running` first, then remaining).
4. For each torrent:
   - load persisted source;
   - add manager to engine;
   - apply save path, selected files, per-torrent settings;
   - apply intent/deferred semantics.
5. Continue runtime sync as read-side refinement only (never override persisted intent).

## 5. Migration Strategy

- Legacy entries without persisted source remain in catalog and are shown in UI.
- Such entries are marked with recoverable rehydration error until source is available.
- New add flow persists source bytes before best-effort runtime apply, preventing new legacy gaps.

## 6. Risks and Constraints

- Must preserve invariants:
  - one torrent -> one UI item;
  - intent stronger than transient runtime state;
  - pre-engine commands are durable;
  - removed torrents do not reappear.
- Main risks:
  - legacy entries with missing source;
  - app/runtime drift during partial rehydration;
  - startup pressure with large catalogs.

## 7. Implementation Phases

1. Remove engine-wide startup restore and enforce empty-engine boot.
2. Add app-owned source store and persist-first add path.
3. Add staged per-torrent rehydration workflow and running-first priority.
4. Keep deferred replay/runtime sync aligned with intent-first policy.
5. Add migration/backfill and drift hardening.

## 8. Verification Strategy

- Unit:
  - running-first planner behavior;
  - rehydration idempotency and missing-source handling;
  - intent-first sync policy.
- Integration:
  - startup path without `RestoreStateAsync`;
  - fast UI restore before runtime readiness;
  - staged rehydration on large catalogs.
- Manual:
  - startup responsiveness;
  - command behavior while rehydration is in progress;
  - restart consistency across repeated launches.

## 9. Documentation Checklist

- [x] `STARTUP_RESTORE_REDESIGN_PLAN.md`
- [x] `TARGET_ARCHITECTURE.md`
- [x] `APPLICATION_CONTRACTS.md`
- [x] `FAILURE_MATRIX.md`
- [x] `REGRESSION_CHECKLIST.md`
- [x] `CURRENT_ARCHITECTURE_STATE.md`
- [x] ADR for startup restore ownership
