# Phase 13 — UI legacy decoupling

## Goal
Remove the remaining direct dependencies of `Presentation` and shell-oriented UI services on the legacy `ITorrentService`.

## What changed

### New application-owned read path
- Added `ITorrentReadModelFeed`
- Added `TorrentReadModelFeed`

This feed wraps the legacy `ITorrentService` event stream and exposes a replayable application-facing read model feed for UI consumers.

### New application-owned engine status boundary
- Added `ITorrentEngineStatusService`
- Added `TorrentEngineStatusService`

This service wraps legacy engine initialization and ready-state notifications.

## Consumers migrated

### Presentation
`TorrentViewModel` no longer depends directly on `ITorrentService`.
It now uses:
- `ITorrentReadModelFeed`
- `ITorrentEngineStatusService`
- `ITorrentWorkflowService`

### App shell
`TrayService` no longer depends directly on `ITorrentService`.
It now uses:
- `ITorrentReadModelFeed`

`AppStartupService` no longer depends directly on `ITorrentService`.
It now uses:
- `ITorrentEngineStatusService`

## Architectural effect
Legacy `ITorrentService` is now hidden one step deeper from UI-oriented code.
The direct dependency line becomes:

- `Presentation/App UI -> Application feeds/services -> legacy ITorrentService`

instead of:

- `Presentation/App UI -> legacy ITorrentService`

## Why this matters
This phase reduces the surface area of the legacy service and prepares the project for later phases where:
- the legacy service can be replaced internally
- UI can continue working against application-facing contracts
- read/write flows become fully product-owned

## Still transitional
- `ITorrentService` still exists
- `TorrentReadModelFeed` and `TorrentEngineStatusService` are transition bridges
- write workflows still partly terminate in legacy service/use-case code
