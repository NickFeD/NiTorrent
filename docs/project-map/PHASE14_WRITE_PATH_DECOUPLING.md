# Phase 14 — Write path decoupling from legacy `ITorrentService`

## Goal
Move application write scenarios behind a dedicated application boundary so that UI and workflows no longer call the legacy torrent facade directly for mutations or preview.

## What changed
- Added `ITorrentWriteService` in `Application`.
- Added `LegacyTorrentWriteService` as a transition-only adapter over `ITorrentService`.
- Moved the following use cases to the new boundary:
  - `AddTorrentUseCase`
  - `StartTorrentUseCase`
  - `PauseTorrentUseCase`
  - `RemoveTorrentUseCase`
  - `ApplyTorrentSettingsUseCase`
- Moved preview loading in `TorrentPreviewFlow` to `ITorrentWriteService`.
- Switched `TorrentDetailsService` from raw `ITorrentService` lookup to `ITorrentReadModelFeed`.

## Why this matters
The project already hid legacy reads/status from UI in earlier phases. This phase does the same for write scenarios inside `Application`, so `ITorrentService` becomes a deeper infrastructure concern instead of a first-class dependency of user workflows.

## Still transitional
`LegacyTorrentWriteService` still delegates to `ITorrentService`. The target architecture is to replace that adapter with command-oriented application services over the new engine boundary and product-owned repository.
