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


## Phase 6 — deferred actions
- Move deferred action storage from implicit command/startup behavior toward explicit application workflow.
- Later split deferred queue persistence out of the legacy catalog bridge.


- Phase 7 completed: application-owned read-side feed and projections (`PHASE7_READ_SIDE.md`).


## Phase 8 — settings
- Move remaining settings consumers away from direct `ITorrentPreferences` edits.
- Later unify torrent, app and shell settings under a broader product settings model without reintroducing UI-to-storage coupling.

- [x] Phase 15 — isolate remaining legacy `ITorrentService` adapters inside Infrastructure

- [x] Phase 16 — engine-owned read/status path

- [x] Phase 17 — engine-owned write path


## Phase 18
- `MonoTorrentService` reduced to a thin compatibility layer over application read/status/write/maintenance services.
- `TorrentMonitor` and runtime settings refresh no longer depend on `ITorrentService`.
