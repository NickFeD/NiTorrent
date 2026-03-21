# NiTorrent — TRANSITION_BACKLOG

## Purpose
Tracks transition-only bridges that must be deleted before the migration to `TARGET_ARCHITECTURE_V2.md` is considered complete.

## Active bridges
- `CatalogBackedTorrentCollectionRepository`
  - Why it exists: new product-owned collection is still stored in legacy `TorrentCatalogStore`.
  - Delete when: dedicated product collection storage replaces legacy catalog-backed bridge.

- `LegacyMonoTorrentEngineAdapter`
  - Why it exists: new engine ports still forward to legacy `ITorrentService`.
  - Delete when: MonoTorrent integration is split into dedicated engine components and `ITorrentService` stops being the engine boundary.

- `TorrentSnapshot`
  - Why it exists: current UI and update flow still consume legacy read model.
  - Delete when: read-side is rebuilt around application projections and `TorrentEntry`-driven queries.


## Phase 5 — commands
- Migrate add/preview commands to new product/domain model.
- Expose product-level command results up to UI.
- Remove direct command dependency on legacy `ITorrentService`.
