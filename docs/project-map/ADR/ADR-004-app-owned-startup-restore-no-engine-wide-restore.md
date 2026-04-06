# ADR-004 App-Owned Startup Restore Without Engine-Wide Restore

Date: 2026-04-06  
Status: Accepted

## Context

Startup restore behavior must prioritize app-owned persistence and predictable UI recovery.
Engine-wide restore from `torrent_engine.dat` via `ClientEngine.RestoreStateAsync` made startup ownership engine-centric.

## Decision

Canonical startup path must:

1. load app-owned catalog and render fast UI state;
2. initialize an **empty** `ClientEngine`;
3. rehydrate torrents in controlled staged order from app-owned source storage;
4. apply user-owned intent/settings during rehydration;
5. keep runtime sync as refinement only.

`ClientEngine.RestoreStateAsync` is excluded from canonical startup restore.

## Consequences

Positive:
- startup source of truth is explicit and product-owned;
- UI restore does not depend on engine state file integrity;
- rehydration order and throttling are controlled by application policy.

Trade-off:
- app must persist and maintain per-torrent source artifacts;
- legacy entries without source require migration/backfill strategy.

## References

- `STARTUP_RESTORE_REDESIGN_PLAN.md`
- `TARGET_ARCHITECTURE.md` section 5.2
- `APPLICATION_CONTRACTS.md` section 5.1
