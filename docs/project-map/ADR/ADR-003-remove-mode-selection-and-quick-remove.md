# ADR-003 Remove Mode Selection and Quick Remove Behavior

Date: 2026-04-05
Status: Accepted

## Context

Remove behavior must support two user-valid outcomes:
1. remove record only;
2. remove record and downloaded files.

No product-level priority exists between these modes.

## Decision

At removal time, user must explicitly choose remove mode.

If quick remove/hotkey is introduced, it must still open a remove-mode choice dialog before execution.
No implicit default remove mode is allowed for shortcut-based removal.

## Consequences

Positive:
- prevents accidental destructive deletes;
- keeps remove semantics explicit and consistent.

Trade-off:
- one extra interaction step for quick-remove flows.

## References

- `USER_APP_LOGIC.md` sections 3.9 and 9.1
- `TARGET_ARCHITECTURE.md` section 11
