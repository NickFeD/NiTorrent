# Project Map

Status: active
Last updated: 2026-04-05

## Source modules

### `src/NiTorrent.Domain`
- domain entities, intent/deferred model, state/policy rules.

### `src/NiTorrent.Application`
- use cases, workflows, contracts/ports, read/write orchestration.

### `src/NiTorrent.Infrastructure`
- MonoTorrent adapters, persistence stores, runtime integration, background coordination.

### `src/NiTorrent.Presentation`
- view models and presentation logic consuming application contracts.

### `src/NiTorrent.App`
- shell/app lifecycle wiring, pages, UI services, platform integrations.

## Documentation modules

### Active architecture set
- `README_ARCHITECTURE.md` (entry point)
- `USER_APP_LOGIC.md`
- `TARGET_ARCHITECTURE.md`
- `APPLICATION_CONTRACTS.md`
- `NFR_SLO.md`
- `FAILURE_MATRIX.md`
- `ANTI_PATTERNS.md`
- `REGRESSION_CHECKLIST.md`
- `OPEN_QUESTIONS.md`
- `ADR/`

### Historical docs
- `archive/` (not source of truth).
