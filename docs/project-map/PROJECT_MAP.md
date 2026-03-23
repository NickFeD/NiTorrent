# Project map

## Активные ключевые узлы

### `src/NiTorrent.Application`
- contracts и use cases
- workflows restore/deferred
- details/settings/write/read orchestration contracts

### `src/NiTorrent.Domain`
- torrent entries, runtime state, deferred actions, projection/status policies

### `src/NiTorrent.Infrastructure`
- catalog store
- runtime registry
- engine startup / command execution / update publication
- infrastructure-backed реализации application ports

### `src/NiTorrent.Presentation` и `src/NiTorrent.App`
- view models и shell services
- только consumption application contracts
