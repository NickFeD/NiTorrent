# Phase 11 — Details Slice UI Wiring

## What was done

- Added `TorrentDetailsPage` in `NiTorrent.App`.
- Wired double-click on torrent items in `TorrentPage` to navigate to details.
- Added page parameter passing via `TorrentId`.
- Bound the page to `TorrentDetailsViewModel`.
- Kept navigation on existing WinUI/DevWinUI shell infrastructure instead of introducing a custom navigation framework.

## Why this matches `NUGET_PACKAGES_MAP.md`

- `CommunityToolkit.Mvvm` remains the MVVM base for the details view model.
- `Microsoft.WindowsAppSDK` / WinUI stay responsible for page and navigation UI.
- No custom MVVM or custom page navigation framework was introduced.

## Transitional notes

- Details page is wired into UI, but per-torrent settings are still only persisted, not yet applied to runtime through the engine boundary.
- Breadcrumb/menu integration remains lightweight and hidden for now.
