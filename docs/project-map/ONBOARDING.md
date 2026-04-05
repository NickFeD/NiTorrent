# Onboarding

Status: active
Last updated: 2026-04-05

## Quick start

1. Read `README_ARCHITECTURE.md` (single entry point).
2. Read `USER_APP_LOGIC.md` and `TARGET_ARCHITECTURE.md`.
3. Read `APPLICATION_CONTRACTS.md` and `ANTI_PATTERNS.md`.
4. Before implementation, check `OPEN_QUESTIONS.md`.
5. Before merge, run through `REGRESSION_CHECKLIST.md`.

## Core implementation flow

- UI -> `ITorrentWorkflowService`
- User commands (`start/pause/remove`) -> `ITorrentCommandService`
- Add/preview/settings apply -> `ITorrentWriteService`
- Startup restore/sync -> restore + sync workflows
- UI read-side -> `ITorrentReadModelFeed`

## Non-negotiable rules

- Do not add a second command pipeline outside intent/deferred model.
- Do not bypass application contracts from UI.
- Do not surface raw infrastructure/engine exceptions to users.
- Do not implement ambiguous behavior without recording and resolving it via `OPEN_QUESTIONS.md`.
