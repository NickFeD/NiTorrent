# NiTorrent — Phase 5: Domain-oriented Commands

## Что сделано
На этом этапе state-changing команды начали переводиться на новую product/domain модель.

Введены:
- `ITorrentCommandService`
- `TorrentCommandService`
- `TorrentCommandResult`
- `TorrentCommandOutcome`
- доменная policy `TorrentEntryCommandPolicy`

## Что уже переведено
Через новый command service теперь проходят:
- `StartTorrentUseCase`
- `PauseTorrentUseCase`
- `RemoveTorrentUseCase`

## Новая логика
Команда теперь работает в терминах новой модели:
1. читает `TorrentEntry` из `ITorrentCollectionRepository`;
2. определяет, доступен ли runtime-fact для этого торрента;
3. меняет `Intent` и `DeferredActions` через доменную policy;
4. сохраняет product state в repository;
5. если engine доступен — делегирует выполнение в `ITorrentEngineGateway`;
6. возвращает product-level result.

## Что ещё остаётся legacy
- `AddTorrentUseCase` и preview/add flow пока ещё не переведены на новую модель;
- `TorrentWorkflowService` пока скрывает переход и не возвращает `TorrentCommandResult` в UI;
- deferred actions уже выражены в домене, но их реальное применение пока ещё завязано на transition workflow.

## Почему это важно
Это первый этап, где команды перестают быть просто thin wrappers над legacy `ITorrentService`.
Теперь они:
- обновляют product-owned state;
- могут быть отложены до готовности engine;
- возвращают result уровня приложения, а не зависят напрямую от engine exceptions.
