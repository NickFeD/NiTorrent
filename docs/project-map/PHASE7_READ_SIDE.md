# Phase 7 — Read-side and projections

## Goal
Stop treating the UI list as a direct mirror of the legacy engine feed. Introduce an application-owned read model feed that projects the product collection plus runtime facts into transition snapshots for presentation.

## What was added
- `ITorrentReadModelFeed`
- `TorrentReadModelFeed`
- `TorrentReadModelProjectionPolicy`

## New read-side model
The list shown to the user is now projected from two sources:
1. `ITorrentCollectionRepository` — product-owned collection
2. `ITorrentRuntimeFactsProvider` — engine runtime facts

The feed merges them and publishes `TorrentSnapshot` as a **transition read model** for the current UI.

## Why this matters
Previously the UI subscribed directly to legacy engine updates. That kept read-side ownership in infrastructure and made cached items, deferred intent and engine startup behavior harder to reason about.

Now the UI list is produced by an application read-side service.

## Transition status
This is still transition architecture because:
- `TorrentSnapshot` remains the read model shape
- `TorrentViewModel` still listens to `ITorrentService.Loaded` for the ready signal
- details and per-torrent projections are not implemented yet

## Next direction
- move more read concerns out of `ITorrentService`
- introduce detail projection for the future torrent details screen
- replace remaining direct UI dependence on legacy service events
