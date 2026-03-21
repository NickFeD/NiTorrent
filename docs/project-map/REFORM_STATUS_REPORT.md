# Refactor status report

## Current pause point before system testing

This report captures the state after:
- `App.xaml.cs` unload and close-flow simplification
- `MonoTorrentService` decomposition
- `TorrentViewModel` thinning
- application-layer consolidation around torrent workflows
- legacy cleanup needed before the broad system test

## What was completed in this pass

### 1. Application-layer consistency pass

The torrent UI and activation flows now converge on application scenarios instead of each caller rebuilding the orchestration itself.

Current high-level shape:
- `ITorrentWorkflowService` is the main torrent scenario entry point for UI and activation
- `ITorrentPreviewFlow` owns preview + confirm + add orchestration
- `TorrentViewModel` calls workflow methods instead of coordinating add/start/pause/remove itself
- `AppActivationService` uses workflow-level file activation flow instead of talking directly to `ITorrentService`

Additional alignment completed in this pass:
- settings application no longer calls `ITorrentService` directly from `TorrentSettingsViewModel`
- `ApplyTorrentSettingsUseCase` now owns the "apply engine settings after preference save" scenario

### 2. Legacy close-dialog cleanup

Removed the old `ShowCloseActionDialogOnClose` setting from the runtime configuration model.

Expected close behavior now:
- main window close respects `MinimizeToTrayOnClose`
- tray "Exit" always performs full shutdown
- no runtime branch uses a close-choice dialog

### 3. Pre-test pause prepared

The codebase is now at a practical pause point for system testing after a final manual review.

## Remaining known risks to validate in testing

1. Close/tray interaction after repeated open-hide-open cycles
2. Immediate UI refresh after torrent commands
3. File activation while the app is cold-starting
4. Restore after previous shutdown and after interrupted shutdown
5. Settings save + engine reapply path

## Files intentionally reviewed for this checkpoint

- `src/NiTorrent.App/App.xaml.cs`
- `src/NiTorrent.App/Services/AppLifecycle/*`
- `src/NiTorrent.Application/Torrents/*`
- `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`
- `src/NiTorrent.Presentation/Features/Settings/TorrentSettingsViewModel.cs`
- `src/NiTorrent.Infrastructure/Torrents/*`
- `src/NiTorrent.Infrastructure/Settings/TorrentConfig.cs`

## Decision

Refactoring should pause after the pre-test checklist is accepted and the full system test is executed.
Only bug fixes found during testing should happen before the next large architectural step.

## Settings system note
- Torrent settings page uses a unified staged-edit model: edit values in the form, then apply with the `Применить` button.
- `MinimizeToTrayOnClose` follows the same save/apply flow as all other settings on the page.
- `nucs.JsonSettings` remains the storage backend; no additional NuGet package is required for settings persistence.

## Architecture transition progress

- Phase 1 introduced a new domain model for product-owned torrent collection.
- Phase 2 introduced `ITorrentCollectionRepository` with a catalog-backed bridge.
- Phase 3 introduced new engine ports with a legacy adapter over `ITorrentService`.
- Phase 4 introduced `RestoreTorrentCollectionWorkflow` as the new application-level startup/restore scenario.


## Phase 5 progress
- Added domain-oriented command layer for start/pause/remove.
- Legacy add/preview flow is still pending migration.
- See `PHASE5_COMMANDS.md`.


## Phase 6
- Added explicit deferred action workflow in Application.
- Restore workflow now builds an execution plan and applies deferred actions through a dedicated workflow instead of inline procedural logic.


## Phase 7 completed: application-owned read-side feed and projections (`PHASE7_READ_SIDE.md`).


## Phase 8 completed: product-owned torrent settings subsystem (`PHASE8_SETTINGS_SUBSYSTEM.md`).


## Phase 9
- Introduced product-owned shell policy for close/tray behavior.
- Close behavior is now resolved via domain policy instead of a raw UI boolean.


## Phase 10
- Introduced a details slice foundation for future double-click navigation.
- Added product-owned per-torrent settings contract and JSON-backed repository.


## Phase 11
- Wired torrent details into the UI with double-click navigation.
- Added `TorrentDetailsPage` backed by `TorrentDetailsViewModel`.
- Preserved existing WinUI/DevWinUI navigation responsibilities instead of introducing custom routing abstractions.
