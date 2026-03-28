# Onboarding

## Сначала прочитать
1. `USER_APP_LOGIC.md`
2. `CURRENT_ARCHITECTURE_STATE.md`
3. `ANTI_PATTERNS.md`
4. `REGRESSION_CHECKLIST.md`

## Живые потоки
- UI -> `ITorrentWorkflowService`
- workflow/use cases -> `ITorrentCommandService` для start/pause/remove
- workflow/use cases -> `ITorrentWriteService` для add/preview/apply-settings
- startup -> `IRestoreTorrentCollectionWorkflow`
- runtime facts -> product projection -> `ITorrentReadModelFeed`
- UI read-side -> `ITorrentReadModelFeed`

## Что не возвращать
- legacy facade `ITorrentService`
- вторую command-систему внутри infrastructure queue/startup recovery
- сырые исключения MonoTorrent в диалоги
- новый JSON/settings стек без явной причины

- Periodic runtime reconciliation проходит через `SyncTorrentCollectionFromRuntimeWorkflow`, а не через snapshot-publisher внутри infrastructure.
