# NiTorrent — PHASE 2: Product-owned collection repository

## Что сделано

В проект введён новый контракт:
- `ITorrentCollectionRepository`

И временная инфраструктурная реализация:
- `CatalogBackedTorrentCollectionRepository`

Также в Domain добавлены элементы новой модели коллекции:
- `TorrentEntry`
- `TorrentKey`
- `TorrentIntent`
- `TorrentRuntimeState`
- `TorrentLifecycleState`
- `DeferredAction`

## Зачем это нужно

Это первый шаг к модели, где:
- пользовательская коллекция торрентов принадлежит продукту;
- startup и restore работают от product collection;
- MonoTorrent больше не определяет, какие торренты “существуют” для приложения.

## Что пока ещё legacy

Пока новый repository хранит данные через старый `TorrentCatalogStore`.
Это сознательный transition-only компромисс.

## Что делать дальше

Следующий шаг:
- перевести startup/restore workflow на чтение именно из `ITorrentCollectionRepository`;
- затем выделить отдельную storage model для `TorrentEntry`.
