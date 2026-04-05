# Failure Matrix

Status: active
Last updated: 2026-04-05
Source references: `USER_APP_LOGIC.md`, `TARGET_ARCHITECTURE.md`

## 1. Purpose

Define expected system behavior for known failure categories so user-facing behavior remains predictable and safe.

## 2. Matrix

| Failure point | User impact | Required system behavior | Recovery path | Logging/Telemetry |
|---|---|---|---|---|
| Catalog load failure at startup | List may be empty or incomplete | App must still open; no hard crash; show safe empty/fallback state | Attempt reload on next startup; optional manual refresh | Error with context (path, exception) |
| Engine initialization failure | No live runtime rates/states | Keep catalog-based UI available; do not block commands | Retry engine init via lifecycle workflow | Error + retry outcome |
| Runtime state file corruption | Runtime restore may fail | Quarantine broken runtime state; continue with safe startup | Start fresh runtime state; keep catalog intent | Warning with backup/quarantine path |
| Runtime sync workflow failure | Stale live projection | Preserve last known projection; continue UI interaction | Next runtime update triggers re-sync attempt | Warning per sync cycle |
| Deferred action apply failure | Command not applied immediately | Keep deferred action persisted; do not lose user intent | Retry when engine is ready or next sync cycle | Warning with torrent id/action |
| Add flow metadata resolve failure (`.torrent`/magnet) | Add action fails | Show clear user message; no partial corrupt entry | User can retry add | User-facing message + error log |
| Duplicate detection conflict | Add denied | Show clear duplicate message; no duplicate entry | None required | Info-level decision log |
| Remove command runtime failure | Item may still exist in runtime | Persist remove intent/deferred action if needed; keep behavior deterministic | Apply later after runtime ready | Warning with delete mode |
| Settings save failure | New settings not applied | Keep old applied settings; show clear message | User can retry save/apply | Error with settings section/context |
| Folder open failure | Folder not opened | Show clear message, keep app stable | User can retry or choose different path | Warning with requested path |
| Tray update/render failure | Tray info may be stale | Do not block UI; tray updates remain best-effort | Continue next update tick | Warning, no crash |
| Shutdown persistence failure | Some runtime state may be missing on next launch | Attempt best-effort shutdown sequence; still allow exit | Recover safely at next startup using catalog + sync | Warning with step name |

## 3. Mandatory Safety Rules

1. No single recoverable failure may crash the app.
2. User intent persistence has priority over immediate runtime success.
3. On ambiguity, prefer safe degradation and clear messaging over hidden partial success.
4. Failures must not create duplicate torrents or phantom runtime-only items.

## 4. Message Policy

1. User messages
- Must be human-readable and localized.
- Must avoid low-level internals by default.

2. Internal logs
- Must contain enough context to diagnose (operation, entity id/path, exception).

## 5. Review Policy

Update this matrix when:
1. a new workflow is added;
2. a known failure mode changes behavior;
3. a postmortem finds a missing or incorrect recovery rule.
