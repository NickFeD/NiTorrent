# Phase 19 — Remove legacy ITorrentService

## Goal
Remove `ITorrentService` and `MonoTorrentService` as runtime dependencies now that UI, shell, read/status, maintenance, and write paths all use the new application boundaries.

## What changed
- Deleted `ITorrentService` from `NiTorrent.Application`.
- Deleted `MonoTorrentService` from `NiTorrent.Infrastructure`.
- Removed `ITorrentService -> MonoTorrentService` registration from infrastructure DI.
- Deleted the unused `LegacyTorrentWriteService` bridge.
- Updated infrastructure comments to describe the new engine-owned path without treating the removed legacy service as an active dependency.

## Why this matters
This is the first phase where the system no longer requires the old service facade to function. The executable paths now go through:
- product-owned repositories
- application-owned feeds/services/workflows
- infrastructure-owned engine pieces and adapters

## Result
`ITorrentService` is no longer part of the active architecture. Remaining migration work can focus on polishing and simplifying the new line instead of keeping the legacy center alive.
