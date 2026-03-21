# Phase 16 — Engine-owned read/status path

## Goal
Start replacing legacy adapter internals with infrastructure-owned components instead of routing read/status/maintenance through `ITorrentService`.

## What changed
- Added `TorrentRuntimeContext` as shared startup/update coordination state.
- `MonoTorrentService` now uses `TorrentRuntimeContext` instead of owning its own gate and command queue.
- Replaced legacy UI feed/status/maintenance adapters with infrastructure-owned implementations:
  - `EngineBackedTorrentReadModelFeed`
  - `EngineBackedTorrentEngineStatusService`
  - `EngineBackedTorrentEngineMaintenanceService`
- The write adapter is still legacy for now (`LegacyTorrentWriteService`).

## Why it matters
This is the first phase where adapter internals stop being thin wrappers over `ITorrentService` and start using infrastructure-owned engine pieces directly:
- `TorrentEventOrchestrator`
- `TorrentUpdatePublisher`
- `TorrentStartupCoordinator`
- `TorrentCatalogStore`
- `TorrentEngineStateStore`

## Result
`Presentation` and shell still depend only on application contracts, but those contracts are now backed by the new execution path for:
- read model updates
- engine startup readiness
- save/shutdown maintenance

## Remaining legacy
The write path still routes through `LegacyTorrentWriteService`.
That becomes the next obvious pressure point for replacing adapter internals with collection/command workflows.
