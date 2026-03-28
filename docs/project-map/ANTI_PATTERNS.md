# Anti-Patterns

## Нельзя
- вести `start/pause/remove` мимо `ITorrentCommandService` и intent/deferred policies;
- возвращать параллельную infrastructure-очередь команд (`TorrentCommandQueue` / queued intent in startup) как второй центр пользовательского intent;
- публиковать UI-истину напрямую из MonoTorrent runtime минуя product-owned collection;
- показывать пользователю сырые `ex.Message` из инфраструктуры;
- вводить новый settings/json subsystem, если задача уже решается через `nucs.JsonSettings` или текущий catalog storage;
- дублировать add/preview logic для `.torrent`, magnet и file-association;
- хранить одно и то же пользовательское правило одновременно в docs и коде с разными трактовками.

## Допустимо
- использовать `TorrentCatalogStore` как persisted product collection / early-start catalog;
- хранить настройки отдельно от каталога runtime-состояния;
- использовать инфраструктурные executors вокруг MonoTorrent, если они не становятся вторым источником пользовательской логики.
