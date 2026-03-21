# NiTorrent — Phase 3: Engine Gateway

## Что сделано
В проект введены новые engine-порты из `TARGET_ARCHITECTURE_V2.md`:

- `ITorrentEngineGateway`
- `ITorrentEngineLifecycle`
- `ITorrentRuntimeFactsProvider`
- `ITorrentEngineStateStore`

Также добавлен transition-only адаптер:

- `LegacyMonoTorrentEngineAdapter`

Он временно скрывает старый `ITorrentService` за новой моделью портов.

## Зачем это нужно
Этап 2 дал проекту product-owned repository для коллекции торрентов.
Следующий шаг — отделить продуктовую коллекцию от движка как от внешней системы.

Теперь `MonoTorrent` начинает восприниматься не как ядро приложения, а как engine adapter за портами.

## Что пока остаётся legacy
- `MonoTorrentService` всё ещё существует;
- use cases и workflows пока ещё могут напрямую зависеть от `ITorrentService`;
- `TorrentSnapshot` остаётся переходной read model для runtime facts.

## Что стало лучше
- появился новый boundary между `Application` и `Infrastructure`;
- можно писать новые workflows поверх engine-портов, не расширяя `MonoTorrentService`;
- transition plan теперь имеет реальную кодовую опору для Этапа 4.

## Что делать дальше
Следующий шаг — не плодить новые use cases на `ITorrentService`, а начать писать startup/restore workflow поверх:

- `ITorrentCollectionRepository`
- `ITorrentEngineLifecycle`
- `ITorrentRuntimeFactsProvider`
- `ITorrentEngineGateway`
- `ITorrentEngineStateStore`

## Критерий удаления transition-only адаптера
`LegacyMonoTorrentEngineAdapter` удаляется, когда:
- application workflows больше не используют `ITorrentService` напрямую;
- `MonoTorrentService` распадается на реальные engine adapters;
- runtime facts перестают строиться через `TorrentSnapshot`.
