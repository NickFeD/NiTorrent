# Phase 10 — Details Slice and Per-Torrent Settings Foundation

## Goal
Prepare the architecture for:
- double click on a torrent row opening a details screen
- future per-torrent settings
- keeping these settings product-owned instead of turning them into engine-only flags or UI-only state

## What changed
- Added `TorrentEntrySettings` to `NiTorrent.Domain`.
- Added `ITorrentEntrySettingsRepository` and `ITorrentDetailsService` to `NiTorrent.Application`.
- Added `TorrentDetailsReadModel` and `TorrentDetailsService`.
- Added `JsonTorrentEntrySettingsRepository` in `NiTorrent.Infrastructure` using existing JSON tooling.
- Added `TorrentDetailsViewModel` in `NiTorrent.Presentation` as the foundation for the future details page.

## Why this follows the package map
- JSON persistence uses the existing `Newtonsoft.Json` + `IAppStorageService` combination.
- No new storage package was introduced.
- MVVM uses existing `CommunityToolkit.Mvvm`; no custom MVVM plumbing was invented.
- UI concerns stay in `Presentation`; persistence stays in `Infrastructure`.

## Transitional notes
- The details slice is not wired into navigation yet.
- Per-torrent settings are intentionally minimal: path override, speed overrides, sequential download flag.
- Runtime application of per-torrent settings is not implemented in this phase; this phase creates the product-owned contract first.

## Expected follow-up
- Introduce details page navigation from double click.
- Apply per-torrent settings through the engine boundary instead of storing them only.
- Split summary read model from details read model when the details page grows.
