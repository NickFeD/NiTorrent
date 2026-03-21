# Phase 18 — Thin compatibility layer

## Goal
Reduce `MonoTorrentService` from a coordination-heavy legacy facade to a thin compatibility layer that only forwards calls to application-owned read/status/write/maintenance services.

## What changed
- `MonoTorrentService` now delegates to:
  - `ITorrentReadModelFeed`
  - `ITorrentEngineStatusService`
  - `ITorrentEngineMaintenanceService`
  - `ITorrentWriteService`
- `ITorrentWriteService` now supports `AddAsync` returning `TorrentId` and explicit `StopAsync`.
- `TorrentMonitor` now refreshes the application read feed instead of calling the legacy facade.
- `TorrentEntrySettingsRuntimeApplier` refreshes the read feed directly instead of depending on `ITorrentService`.

## Architectural impact
This phase turns `ITorrentService` into a true legacy boundary for remaining consumers. It is no longer the owner of orchestration, startup, read updates, or write execution.

## Remaining legacy status
- `ITorrentService` still exists for compatibility.
- Future phases can delete it once no consumers remain.
