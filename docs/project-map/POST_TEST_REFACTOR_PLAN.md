# Post-test refactor plan

This file preserves the next refactor steps so they are not lost while the team pauses for full-system testing.

## Rule for after the pause

Before resuming structural refactoring:
1. execute the full system test checklist
2. fix functional regressions found by the test
3. only then resume the steps below

## Next architectural zones after testing

### A. Normalize read/update flow around torrents
- decide and document the dominant model: push, poll, or hybrid with explicit rules
- review `TorrentMonitor`, `TorrentUpdatePublisher`, tray speed updates, and VM event handling
- reduce hidden coupling between command execution and UI refresh

### B. Review tray and shell lifecycle boundaries
- revisit `TrayService` responsibilities
- remove any remaining shell-specific orchestration leaking across services
- check blocking calls in tray update paths and replace them with safer async flow if needed

### C. Clean up `MonoTorrentService` facade polish
- decide whether remaining facade/event glue is acceptable
- reduce leftover orchestration only if it measurably improves readability
- avoid further decomposition just for the sake of decomposition

### D. Strengthen application contracts
- standardize use-case shapes and naming
- decide where workflow facades are enough and where dedicated query services are clearer
- keep presentation dependent on application scenarios rather than infrastructure details

### E. Prepare testable scenario seams
- identify the smallest set of scenarios worth unit or integration tests
- focus first on startup/restore, add preview flow, close/tray behavior, and settings apply

## Explicitly deferred until after testing
- any further shell/lifecycle redesign
- large changes to tray UI behavior
- monitor/update model redesign
- broad cleanup renames without behavioral value


## Settings system note
- Torrent settings page uses a unified staged-edit model: edit values in the form, then apply with the `Применить` button.
- `MinimizeToTrayOnClose` follows the same save/apply flow as all other settings on the page.
- `nucs.JsonSettings` remains the storage backend; no additional NuGet package is required for settings persistence.
