# NiTorrent — Phase 4: Restore workflow in Application

## Goal
Move startup/restore thinking out of infrastructure init code and into an explicit application workflow.

## What was added
- `IRestoreTorrentCollectionWorkflow`
- `RestoreTorrentCollectionWorkflow`
- `RestoreTorrentCollectionResult`
- `TorrentCollectionRestorePolicy`

## Why this matters
The project now has an application-level place that describes startup in product terms:
1. load product-owned collection;
2. start engine;
3. read runtime facts;
4. sync collection through domain policies;
5. apply intents and deferred actions;
6. persist updated collection.

## Current limitation
This workflow is not yet the runtime path used by the app. It is introduced as the new architectural line and will replace legacy startup in a later migration step.
