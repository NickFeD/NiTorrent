# Target Architecture

Status: **active target architecture**.
Last updated: **2026-04-06**.

This document defines the architecture required to implement the behavior described in `USER_APP_LOGIC.md` reliably, predictably, and with low change cost over time.

Companion documents:
- `NFR_SLO.md`
- `FAILURE_MATRIX.md`
- `APPLICATION_CONTRACTS.md`
- `ADR/README.md`

## 1. Architecture Intent

The system is organized around user scenarios, stable application boundaries, and explicit source-of-truth models.

Primary goals:
- preserve user intent across startup, restart, and partial engine availability;
- keep UI responsive while engine/runtime work happens asynchronously;
- keep user-facing behavior stable even when engine internals are complex;
- make new features additive instead of requiring cross-layer rewrites.

## 2. Scope and Non-Goals

In scope:
- torrent lifecycle from add to remove;
- startup restore and cache/runtime synchronization;
- deferred command execution before engine readiness;
- settings read/edit/save/apply;
- shell behavior (window close, tray restore, tray exit);
- user-facing state projection and message policy.

Out of scope for this version:
- deep per-file priority management beyond current add-preview selection;
- advanced engine diagnostics UX;
- protocol-level design details not yet product-approved.

## 3. Layered Boundaries

## 3.1 Domain Layer

Domain owns business meaning and invariants.

Core responsibilities:
- `TorrentEntry` and related domain state;
- user intent model (running, paused, remove actions);
- deferred action model and conflict policy;
- duplicate detection policy;
- lifecycle/status mapping policy;
- collection restore policy.

Domain must not depend on:
- MonoTorrent runtime types;
- UI framework concerns;
- infrastructure storage details.

## 3.2 Application Layer

Application orchestrates use cases and workflows through explicit ports.

Core responsibilities:
- use cases (`add/start/pause/remove/open folder/apply settings`);
- startup workflows (`restore`, `sync runtime`, `apply deferred actions`);
- shell workflows (`window close`, `tray exit`);
- read-side projection contracts;
- write-side command contracts;
- engine lifecycle/status/maintenance contracts.

Application depends on domain and abstract ports only.

## 3.3 Infrastructure Layer

Infrastructure adapts external systems to application ports.

Core responsibilities:
- MonoTorrent integration;
- catalog and engine-state persistence;
- runtime registry and background orchestration;
- source preparation for `.torrent` and magnet;
- filesystem/OS integrations (folder open, dialogs, pickers, URI launch);
- resilience for corrupted engine state and safe degradation.

Infrastructure must not redefine domain rules.

## 3.4 Presentation and App Layer

Presentation/App consume application contracts only.

Core responsibilities:
- page/view-model composition;
- user interaction handling;
- rendering projected states;
- local form editing state;
- app lifecycle wiring (activation/startup/shutdown);
- tray integration wiring.

Presentation must not call infrastructure implementations directly.

## 4. Source of Truth Model

The architecture uses multiple sources of truth with clear ownership:

1. Product-owned torrent collection (persistent catalog)
- authoritative for list identity, user intent, and persisted user decisions.

2. Runtime engine state
- authoritative for live transfer facts (rates/progress/runtime state).
- startup must initialize an empty engine instance; engine-wide restore APIs are not part of canonical startup restore.

3. Projected user read model
- authoritative for what UI renders at a moment in time.
- built from catalog + synchronized runtime facts.

Rule: runtime facts refine displayed state but must not silently override persisted user intent.

## 5. Canonical User Flows

## 5.1 Unified Add Flow (`.torrent` and magnet)

Canonical flow:
1. prepare source;
2. resolve metadata;
3. normalize to one prepared torrent representation;
4. run duplicate validation;
5. show one preview/validation UX;
6. confirm and create domain entry;
7. add to engine + persist collection;
8. publish read model update.

Requirement: magnet and `.torrent` must converge into the same logic path as early as reasonably possible after metadata becomes available.

## 5.2 Startup and Restore Flow

Canonical flow:
1. load persisted collection immediately for fast UI;
2. initialize **empty** engine in background (without `ClientEngine.RestoreStateAsync`);
3. run staged per-torrent rehydration from app-owned sources (running intent first);
4. synchronize runtime facts;
5. apply deferred actions;
6. refresh projections;
6. keep UI stable (no phantom duplicates, no random disappearance, no intent loss).

## 5.3 Command Flow (Start/Pause/Remove)

Canonical flow:
1. accept command at application boundary;
2. persist intent/deferred action immediately;
3. try immediate runtime apply;
4. if runtime not ready, keep command deferred;
5. apply deferred when runtime becomes available;
6. project final user-facing status.

## 5.4 Close and Tray Flow

Rules:
- main window close action follows setting `Minimize to tray on close`;
- tray `Exit` always performs full app exit;
- explicit tray exit ignores close-behavior setting;
- shutdown path persists required state and closes engine lifecycle safely.

## 6. User-Facing State Model

Architecture must expose one coherent user state model.

Minimum projected states:
- waiting for client initialization;
- fetching metadata;
- checking data;
- downloading;
- seeding;
- paused/stopped (single user-facing state);
- error.

Rule: internal runtime states may be richer, but projection must remain user-stable and deterministic.

## 7. Invariants and Enforcement

Mandatory invariants:
- one torrent appears once in UI;
- duplicate add never creates second entry;
- list survives restart unless user removed item;
- user intent survives startup transitions;
- pre-engine commands are never lost;
- settings page uses consistent edit/save/apply pattern;
- explicit tray exit is always stronger than window close behavior;
- UI must remain responsive during startup, settings navigation, transfers, and tray updates.

Enforcement strategy:
- domain policy classes enforce intent/deferred/duplicate rules;
- application workflows enforce order-of-operations;
- infrastructure adapters enforce safe runtime/persistence boundaries;
- presentation consumes projection outputs and avoids ad hoc state derivation.

## 8. Extension Strategy

Architecture is designed for low-friction extension through:
- additive use cases and workflows behind stable contracts;
- replacing infrastructure adapters without changing domain rules;
- extending projection policy without changing UI wiring contracts;
- adding settings pages with the same read/edit/save/apply template;
- introducing new activation modes through application-level flows.

When adding new features:
1. add/adjust domain rule first;
2. define or update application contract;
3. implement infrastructure adapter;
4. wire presentation via application contract only;
5. update architecture state docs and regression checklist.

## 9. Governance Rules

1. UI scenarios must not reference infrastructure classes directly.
2. New workflows must not reintroduce compatibility facades.
3. One contract should have one active implementation line (temporary dual-lines must be explicit and time-bounded).
4. Migration-only adapters must be removed after replacement.
5. Architectural status is tracked in `CURRENT_ARCHITECTURE_STATE.md`.
6. Any behavior change affecting `USER_APP_LOGIC.md` requires architecture doc update in the same change set.
7. If a requirement is ambiguous, implementation must not invent behavior that is not explicitly approved.
8. Ambiguities must be recorded and resolved through a documented clarification before coding the affected behavior.

## 10. Mapping to USER_APP_LOGIC

Coverage map:
- User scenarios (`USER_APP_LOGIC` section 3): covered by canonical flows in section 5.
- Pre-engine actions (`USER_APP_LOGIC` section 4): covered by deferred command flow in section 5.3.
- User-facing states (`USER_APP_LOGIC` section 5): covered by section 6.
- Invariants (`USER_APP_LOGIC` section 6): covered by section 7.
- Hidden internal complexity (`USER_APP_LOGIC` section 7): covered by section 4 + projection rules.
- Error/message expectations (`USER_APP_LOGIC` section 8): enforced at application boundary and infrastructure adapter mapping.
- Known product-level open areas (`USER_APP_LOGIC` section 9): tracked in section 11 below.

## 11. Open Product Decisions (Explicitly Tracked)

1. Magnet activation scope
- Magnet handling is supported only through UI input and internal application scenarios.
- System-level `magnet:` protocol activation is out of scope unless explicitly approved in a future product decision.

2. Quick remove UX
- For future hotkey/quick remove actions, the app must always show a removal-mode selection dialog.
- No implicit default mode is allowed for shortcut-driven removal.

3. Future per-file priority model
- If added, define dedicated domain model + application use case + projection contract before UI work.

## 12. Clarification Protocol (No Unplanned Behavior)

When uncertainty appears, use this protocol:

1. Stop and isolate ambiguity
- Identify exactly which requirement text allows multiple interpretations.

2. Record the question
- Add the ambiguity and options to `OPEN_QUESTIONS.md` with impacted scenarios/contracts.

3. Ask for product clarification
- Do not implement speculative behavior while the decision is unresolved.

4. Apply only approved behavior
- After clarification, update `USER_APP_LOGIC.md`, this document, and any affected companion docs/ADRs in the same change set.

---

This document replaces the obsolete placeholder in `TARGET_ARCHITECTURE.md` and supersedes `TARGET_ARCHITECTURE_V2.md` as the primary target architecture reference.
