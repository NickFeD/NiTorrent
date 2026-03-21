# Stablization audit: App / Shell

## Scope

This audit covers the shell-facing layer:
- `App.xaml.cs`
- `AppStartupService`
- `AppCloseCoordinator`
- `AppShutdownCoordinator`
- `MainWindowLifecycle`
- `TrayService`

## Main finding

The shell still depended directly on raw torrent runtime through `ITorrentService`.
That created tight coupling between:
- window and tray behaviour
- shell startup
- shutdown/minimize save path
- torrent engine runtime

This coupling violated the intended direction from:
- `TARGET_ARCHITECTURE_V2.md`
- `ANTI_PATTERNS.md`
- stabilization layer plans

## Problems found before the fix

### 1. `AppStartupService` depended on `ITorrentService`
Problem:
- shell startup called torrent engine initialization directly
- shell knew too much about runtime ownership

Risk:
- shell startup and engine startup stayed entangled
- future migration away from `ITorrentService` would keep breaking the shell

### 2. `AppCloseCoordinator` depended on `ITorrentService`
Problem:
- close policy and runtime maintenance were mixed together
- save-before-tray was executed through the raw torrent runtime boundary

Risk:
- close flow remained engine-centric instead of product-rule-centric

### 3. `AppShutdownCoordinator` depended on `ITorrentService`
Problem:
- shutdown sequence invoked torrent runtime directly
- shell shutdown could not be reasoned about independently from engine implementation

### 4. `TrayService` depended on `ITorrentService`
Problem:
- tray UI subscribed directly to torrent runtime updates
- shell UI and torrent read model were mixed

Risk:
- shell could not be stabilized independently
- read-side changes in torrent runtime leaked straight into tray behaviour

## Fix applied in the stabilization pass

Direct shell dependencies on `ITorrentService` were removed.

New application-facing ports were introduced:
- `ITorrentReadModelFeed`
- `ITorrentEngineStatusService`
- `ITorrentEngineMaintenanceService`

Temporary infrastructure adapters were added:
- `LegacyTorrentReadModelFeed`
- `LegacyTorrentEngineStatusService`
- `LegacyTorrentEngineMaintenanceService`

This means:
- shell now depends on application-facing ports
- infrastructure still bridges to the current runtime implementation
- migration can continue without re-breaking the shell every time the runtime changes

## Current state after the fix

### `AppStartupService`
Uses:
- `ITorrentEngineStatusService`

### `AppCloseCoordinator`
Uses:
- `ITorrentEngineMaintenanceService`
- `ITorrentPreferences`
- `IMainWindowLifecycle`

### `AppShutdownCoordinator`
Uses:
- `ITorrentEngineMaintenanceService`
- `IMainWindowLifecycle`

### `TrayService`
Uses:
- `ITorrentReadModelFeed`
- `IUiDispatcher`

## What is still not ideal

These are not blockers for the current stabilization package, but they still remain:

1. `ITorrentPreferences` is still used directly in shell close policy.
   In the future this should move behind a dedicated shell/product settings service.

2. `TrayService` still calculates speed summary itself.
   Later this can move behind a dedicated projection/summary service.

3. `App.xaml.cs` still acts as a large composition root and host bootstrapper.
   This is acceptable for now, but should stay under review.

## Outcome

The shell layer is no longer directly coupled to raw torrent runtime.
This is the first stabilization package for App / Shell and should be treated as the baseline before deeper cleanup.
