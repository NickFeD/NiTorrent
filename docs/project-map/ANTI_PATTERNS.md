# Anti-Patterns

## Forbidden
- bypass `ITorrentCommandService` and intent/deferred policies for `start/pause/remove`;
- reintroduce a parallel infrastructure command queue (`TorrentCommandQueue` / startup queued intent) as a second user-intent center;
- publish UI truth directly from MonoTorrent runtime, bypassing the product-owned collection;
- show raw infrastructure `ex.Message` values to users;
- introduce a second source of truth for settings (new settings/json subsystem without an explicit architecture migration);
- duplicate add/preview logic across `.torrent`, magnet, and file-association flows;
- keep the same user rule with conflicting interpretations across docs and code;
- implement `quick remove/hotkey` without the removal-mode selection dialog;
- add system-level `magnet:` activation without a separate approved product decision;
- invent behavior for ambiguous requirements without recording the question in `OPEN_QUESTIONS.md`.

## Allowed
- use `TorrentCatalogStore` as the persisted product collection / early-start catalog;
- keep settings storage separate from runtime-state catalog storage;
- use infrastructure executors around MonoTorrent as long as they do not become a second source of user logic.
