# NiTorrent — TRANSITION_BACKLOG

Этот файл фиксирует временные мосты, которые допустимы только на время перехода к `TARGET_ARCHITECTURE_V2.md`.

## Активные transition-only мосты

### 1. `CatalogBackedTorrentCollectionRepository`
- Новый `ITorrentCollectionRepository` уже введён.
- Но его реализация пока сидит на старом `TorrentCatalogStore`.
- Это допустимо только до этапа выделения отдельного product-owned storage.

**Критерий удаления:**
- появляется отдельная storage model для `TorrentEntry`;
- `TorrentCatalogStore` перестаёт быть источником product collection.

### 2. `TorrentSnapshot` как ранняя read model
- Snapshot остаётся для UI и engine monitoring.
- Но он не должен стать ядром доменной модели.

**Критерий удаления как central model:**
- read-side строится из `TorrentEntry` + runtime facts.

### 3. Legacy `ShouldRun`
- В старом каталоге он ещё используется.
- В новой модели его заменяет `TorrentIntent` и future deferred actions.

**Критерий удаления:**
- startup/restore и commands переходят на `TorrentEntry.Intent`.
