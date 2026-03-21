# NiTorrent — Phase 6: Explicit Deferred Actions

## Что сделано
Deferred actions перестали быть неявочным procedural куском startup-кода.

Введён отдельный workflow:
- `IApplyDeferredTorrentActionsWorkflow`
- `ApplyDeferredTorrentActionsWorkflow`
- `ApplyDeferredTorrentActionsResult`

## Что это меняет
Теперь отложенные действия трактуются как отдельный execution step:
1. workflow получает `TorrentEntry` с `DeferredActions`;
2. определяет, доступен ли runtime для конкретного торрента;
3. применяет действия в порядке `RequestedAtUtc`;
4. очищает очередь после успешного применения;
5. сообщает, какие записи были обновлены, удалены или остались отложенными.

## Почему это важно
Это приближает проект к `USER_APP_LOGIC.md`:
- действия до готовности engine — нормальная часть продукта;
- `Start/Pause/Remove` до готовности engine не выглядят как особые обходные пути;
- startup/restore больше не должен сам вручную разбирать каждый deferred action.

## Что переведено на новый механизм
- `RestoreTorrentCollectionWorkflow` теперь строит execution plan и делегирует применение deferred actions в отдельный workflow.

## Что ещё остаётся переходным
- команды всё ещё сами формируют deferred actions через `TorrentEntryCommandPolicy`;
- репозиторий пока не имеет отдельного storage slice для deferred queue;
- UI пока не видит deferred action queue как отдельную projection.

## Критерий этапа
Deferred actions стали отдельной частью application execution model, а не процедурной логикой внутри startup/restore.
