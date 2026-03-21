# Phase 12 — Apply per-torrent settings to runtime

## Goal

Make per-torrent settings part of the real execution path, not only a saved JSON document.

## What changed

### New application port

- `ITorrentEntrySettingsRuntimeApplier`

This port belongs to the application boundary. It expresses a product-level need:

- when the user saves settings for a specific torrent,
- the system should try to apply them to the running torrent immediately,
- while keeping infrastructure-specific engine details outside of `Application` and `Presentation`.

### Transitional infrastructure adapter

- `TorrentEntrySettingsRuntimeApplier`

This adapter applies settings to a live MonoTorrent manager through reflection-based engine bridging.

Why reflection is acceptable here:
- this is a **transition-only** bridge,
- it avoids leaking MonoTorrent-specific API shapes into the application contracts,
- it allows the new architecture to keep the engine behind an adapter while the runtime boundary is still being migrated.

### What is applied immediately

For a currently loaded torrent manager, the bridge tries to apply:
- per-torrent download rate limit
- per-torrent upload rate limit
- sequential download flag

### What is not fully runtime-applied yet

- `DownloadPathOverride`

This setting is persisted as part of the product model, but dynamic file relocation is still a later step.
For now it is treated as saved intent, not guaranteed immediate runtime change.

## UI effect

The details page now saves settings through the application service and informs the user that the settings were:
- saved
- applied where possible without restart

This keeps the UX honest while the transition is still in progress.

## Architectural effect

This phase is important because it moves per-torrent settings from:
- "stored configuration only"

to:
- "product rule with runtime application attempt"

without pulling MonoTorrent API details into `Presentation` or `Application`.
