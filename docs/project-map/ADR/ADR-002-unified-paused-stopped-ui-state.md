# ADR-002 Unified Paused and Stopped UI State

Date: 2026-04-05
Status: Accepted

## Context

Runtime engine states may distinguish `Paused` and `Stopped`, but user-facing behavior requires one coherent and stable state model.

## Decision

UI projection must represent internal `Paused` and `Stopped` as one unified user-facing state (`paused/stopped`).

Internal state distinction may remain in runtime and domain internals when needed for execution logic.

## Consequences

Positive:
- simpler user mental model;
- reduced UI ambiguity during startup/sync transitions;
- fewer edge-case regressions in status rendering.

Trade-off:
- some internal nuance is intentionally hidden from the user.

## References

- `USER_APP_LOGIC.md` sections 3.8 and 5
- `TARGET_ARCHITECTURE.md` section 6
