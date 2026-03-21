# Phase 8 — settings as a product-owned subsystem

## Goal
Stop treating torrent settings as a direct UI wrapper over `ITorrentPreferences` and move them toward a product-owned flow:

1. load settings draft
2. edit draft in UI
3. save draft through an application service
4. apply runtime effects after persistence

## What changed

### New domain model
- `TorrentSettingsDraft`
- uses `AppCloseBehavior` instead of a dedicated `bool` for future compatibility with `AskUser`

### New application ports
- `ITorrentSettingsRepository`
- `ITorrentSettingsService`

### New application service
- `TorrentSettingsService`
  - loads draft from repository
  - saves draft to repository
  - applies runtime engine settings through `ITorrentService.ApplySettingsAsync()` as a temporary bridge

### New infrastructure implementation
- `TorrentConfigSettingsRepository`
  - maps between `TorrentConfig` and `TorrentSettingsDraft`
  - keeps `nucs.JsonSettings` as the storage backend

### Presentation changes
- `TorrentSettingsViewModel` no longer edits `ITorrentPreferences` directly
- the page now works with a product-owned draft model and a single save/apply entry point

## Why this matters
This moves settings closer to the target architecture:
- UI edits a draft, not storage primitives
- persistence belongs to a repository
- apply side effects happen through an application service
- close behavior already fits future expansion to `AskUser`

## What is still transitional
- `ITorrentPreferences` still exists for legacy consumers such as current close/runtime code
- runtime apply still goes through legacy `ITorrentService.ApplySettingsAsync()`
- app/theme/update settings are still separate slices and are not yet unified into a larger product settings subsystem

## Delete later
- direct preference-based settings flows once all consumers move to the new settings subsystem
