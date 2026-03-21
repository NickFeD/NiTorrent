# Phase 15 — Legacy adapter isolation

## Goal
Push the remaining raw `ITorrentService` usage out of `Application` and keep it behind Infrastructure adapters.

## Why
After Phase 14, UI and application workflows no longer referenced `ITorrentService` directly, but `Application` still contained implementation classes that wrapped the legacy service:
- read model feed
- engine status service
- engine maintenance service
- write service

That kept part of the legacy MonoTorrent integration inside the wrong layer.

## What changed
These implementations were moved from `NiTorrent.Application` to `NiTorrent.Infrastructure/Torrents/LegacyAdapters`:
- `LegacyTorrentReadModelFeed`
- `LegacyTorrentEngineStatusService`
- `LegacyTorrentEngineMaintenanceService`
- `LegacyTorrentWriteService`

`Application` keeps only the contracts:
- `ITorrentReadModelFeed`
- `ITorrentEngineStatusService`
- `ITorrentEngineMaintenanceService`
- `ITorrentWriteService`

## Registration
All legacy-adapter registrations now happen in `AddNiTorrentInfrastructure()`.
`App.xaml.cs` no longer registers those adapter implementations directly.

## Architectural effect
This does **not** remove `ITorrentService` yet.
It does make the layering cleaner:
- `Application` = contracts and workflows
- `Infrastructure` = legacy MonoTorrent adapters and storage/engine concerns
- `App` = composition root, not a place where legacy adapter plumbing is assembled manually

## Result
The remaining legacy dependency is now concentrated deeper in Infrastructure, making the next phase safer:
replace adapter internals with new domain/application ports instead of `ITorrentService`.
