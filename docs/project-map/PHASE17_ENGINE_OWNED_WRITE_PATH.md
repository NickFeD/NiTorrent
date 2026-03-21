# Phase 17 — engine-owned write path

## Goal
Stop routing application write scenarios through the legacy `ITorrentService` facade.

## What changed
- `ITorrentWriteService` is now backed by `EngineBackedTorrentWriteService`.
- Add/start/pause/remove/apply-settings scenarios execute directly against infrastructure-owned engine components:
  - `TorrentStartupCoordinator`
  - `TorrentRuntimeContext`
  - `TorrentCommandExecutor`
  - `TorrentAddExecutor`
  - `TorrentSourceResolver`
  - `TorrentSettingsApplier`
  - `TorrentEventOrchestrator`
  - `TorrentEngineStateStore`
- Legacy `ITorrentService` remains only as a transition facade, not as the required write path for application use cases.

## Why this matters
This is the first step where write scenarios stop being “just hidden legacy calls” and start using the same engine/runtime pieces that already power the infrastructure-owned read/status path.

## Still transitional
- `MonoTorrentService` still exists.
- `EngineBackedTorrentWriteService` still mirrors some legacy orchestration patterns.
- Full replacement of the legacy facade is still a later step.
