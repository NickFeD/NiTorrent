# ADR-001 Unified Magnet and Torrent Add Flow

Date: 2026-04-05
Status: Accepted

## Context

`USER_APP_LOGIC.md` requires magnet and `.torrent` add behavior to be equivalent after metadata becomes available.

## Decision

The system must normalize magnet and `.torrent` inputs into one prepared torrent representation as early as reasonably possible.

From normalization onward, both sources must use one shared path:
1. duplicate validation;
2. preview/validation UX;
3. confirmation and add execution;
4. projection update.

Magnet handling scope is limited to UI input and internal scenarios.
System-level `magnet:` protocol activation is out of scope unless explicitly approved later.

## Consequences

Positive:
- one canonical add path;
- lower duplicate logic drift;
- easier testing and maintenance.

Trade-off:
- preparation layer must reliably resolve magnet metadata before entering canonical flow.

## References

- `USER_APP_LOGIC.md` section 3.4
- `TARGET_ARCHITECTURE.md` section 5.1
