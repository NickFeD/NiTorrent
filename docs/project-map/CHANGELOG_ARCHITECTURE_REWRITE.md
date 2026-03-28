# Architecture rewrite changelog

## Code changes
- Removed dead legacy classes that still referenced deleted `ITorrentService`.
- Removed duplicated domain policy classes from `Domain/Torrents/Policies`.
- Removed duplicate read-model feed contracts/implementations in `Application`.
- Added infrastructure-backed implementations for:
  - `ITorrentRuntimeFactsProvider`
  - `ITorrentEngineGateway`
  - `ITorrentEngineLifecycle`
  - `ITorrentEngineStateStore`
- Updated infrastructure DI registrations to use the new non-legacy path.
- Updated `TorrentSettingsService` to apply settings through `ITorrentWriteService` instead of the removed legacy facade.

## Documentation changes
- Added `CURRENT_ARCHITECTURE_STATE.md` as the current source of truth.
- Rewrote `TARGET_ARCHITECTURE_V2.md`, `REFORM_STATUS_REPORT.md`, `TRANSITION_BACKLOG.md`, `ONBOARDING.md`, `PROJECT_MAP.md`, `README_ARCHITECTURE.md`.
- Marked old phase and migration-plan documents as archived historical notes.
- Included `USER_APP_LOGIC.md` inside `docs/project-map` for a self-contained archive.

## Validation note
This environment does not provide the `dotnet` CLI, so I could not run a full compile/test pass here.

- Removed the unused command/restore/deferred workflow branch so the project keeps a single live write path.
- Moved per-torrent settings storage to a `nucs.JsonSettings`-backed config.
- Normalized user-facing error messages for torrent add/start/pause/remove, settings apply and file activation.
