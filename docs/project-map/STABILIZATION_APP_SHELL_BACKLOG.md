# App / Shell stabilization backlog

## Status

Package 1 completed:
- direct shell dependencies on `ITorrentService` removed
- shell moved to application-facing ports

## Completed in package 1

- [x] `AppStartupService -> ITorrentEngineStatusService`
- [x] `AppCloseCoordinator -> ITorrentEngineMaintenanceService`
- [x] `AppShutdownCoordinator -> ITorrentEngineMaintenanceService`
- [x] `TrayService -> ITorrentReadModelFeed`
- [x] temporary infrastructure adapters registered in `NiTorrent.Infrastructure`

## Next shell stabilization packages

### Package 2 — shell settings normalization
- [ ] move close behavior from raw `ITorrentPreferences` to dedicated shell settings service
- [ ] make close policy product-owned instead of bool-driven shell logic
- [ ] prepare future return of close-choice dialog without reintroducing shell chaos

### Package 3 — tray cleanup
- [ ] move torrent speed aggregation out of `TrayService`
- [ ] keep tray as shell UI only
- [ ] make tray less dependent on raw torrent snapshot semantics

### Package 4 — host/bootstrap cleanup
- [ ] reduce feature registration noise inside `App.xaml.cs`
- [ ] group application service registrations more clearly
- [ ] review startup flow boundaries between host start, engine init, and activation

## Notes

This backlog must be read together with:
- `STABILIZATION_MASTER_PLAN.md`
- `STABILIZATION_LAYER_PLANS.md`
- `ANTI_PATTERNS.md`
- `TARGET_ARCHITECTURE_V2.md`
