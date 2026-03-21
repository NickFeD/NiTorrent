# Phase 9 — Shell Policy as Product Rule

## Goal
Move close/tray behavior from a UI boolean convention to a product-owned shell policy.

## What changed
- Added `AppCloseBehavior`, `AppShellSettings`, `AppShellClosePolicy` to `NiTorrent.Domain`.
- Added `IAppShellSettingsRepository` and `IAppShellSettingsService` to `NiTorrent.Application`.
- Added `JsonAppShellSettingsRepository` in `NiTorrent.Infrastructure` backed by existing `TorrentConfig` and `Nucs.JsonSettings`.
- Updated `AppCloseCoordinator` to resolve close behavior through domain policy instead of reading `ITorrentPreferences.MinimizeToTrayOnClose` directly.
- Updated `TorrentSettingsViewModel` to save close behavior through `IAppShellSettingsService`.

## Why this matches the new architecture
- WinUI/WinUIEx remain in `NiTorrent.App` only.
- Settings storage remains in infrastructure and reuses the existing settings package instead of inventing a new storage mechanism.
- The rule _"window close respects user preference, tray exit always exits"_ now lives as a domain/application concern.

## Transitional notes
- `TorrentConfig.MinimizeToTrayOnClose` is still written for backward compatibility.
- `TorrentConfig.CloseBehavior` is the new forward-looking persistence field.
- `AskUser` is part of the domain now, but the UI for that behavior is intentionally not implemented yet.
