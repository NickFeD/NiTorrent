# NiTorrent — ARCHITECTURE_TRANSITION_PLAN

## Цель
Этот документ описывает **этапы перехода** от текущего проекта к архитектуре из `TARGET_ARCHITECTURE_V2.md`.

Это не план косметического рефакторинга. Допускается, что некоторые этапы:
- временно ломают код;
- требуют миграции моделей;
- меняют storage contracts;
- потребуют удалить часть уже написанных переходных фасадов.

Главное требование:

> В финале должен получиться рабочий проект, который соответствует `USER_APP_LOGIC.md`, не содержит лишних временных прослоек и не держит бизнес-логику в infrastructure.

---

## 0. Что меняется по сравнению с прежним подходом
Старый путь улучшал текущую архитектуру.
Новый путь исходит из того, что **текущая архитектура не является целевой**.

Поэтому миграция теперь строится не вокруг вопроса «как аккуратно допилить существующие сервисы», а вокруг вопроса:

**как перейти от engine-centered модели к product-centered модели**.

Это означает:
- `MonoTorrentService` не должен пережить миграцию как главный фасад;
- `TorrentSnapshot` не должен остаться ядром доменной модели;
- `ShouldRun` как одиночный флаг должен уступить место более полной модели намерения и deferred actions;
- любой transition-only код должен иметь план удаления.

---

## Общие правила перехода
1. Не переписывать всё одним PR.
2. На каждом этапе фиксировать:
   - что объявляется legacy;
   - что становится новым источником истины;
   - какие мосты временные;
   - когда эти мосты должны быть удалены.
3. Каждый bridge-компонент должен быть:
   - явно помечен как transition-only;
   - отмечен в docs;
   - иметь критерий удаления.
4. Нельзя переносить anti-patterns из старой архитектуры в новую.
5. Нельзя создавать новый центральный фасад «на время», который потом станет вторым `MonoTorrentService`.
6. Любой новый слой должен доказывать свою необходимость через `USER_APP_LOGIC.md`, а не через теоретическую «чистоту».

---

## Этап 0. Зафиксировать продуктовую спецификацию и запреты
### Цель
Закрепить, что именно считается правильным поведением.

### Что делаем
- считаем `USER_APP_LOGIC.md` продуктовой спецификацией;
- поддерживаем `REGRESSION_CHECKLIST.md`;
- поддерживаем `ANTI_PATTERNS.md`;
- создаём `TRANSITION_BACKLOG.md` для временных мостов;
- маркируем существующие transition-only компоненты.

### Результат
Есть понятный критерий, что должно работать и что запрещено архитектурно.

---

## Этап 1. Ввести новую доменную модель коллекции торрентов
### Проблема сейчас
Domain слишком тонкий и не выражает продуктовые правила.

### Что делаем
Создаём новые доменные элементы:
- `TorrentEntry`
- `TorrentIntent`
- `TorrentLifecycleState`
- `TorrentRuntimeState`
- `TorrentKey`
- `DeferredAction`
- `AppCloseBehavior`
- `GlobalTorrentSettings`
- future `PerTorrentSettings`

Выносим в Domain:
- duplicate policy;
- restore policy;
- status resolver;
- deferred action policy;
- settings validation rules.

### Что пока оставляем
- `TorrentSnapshot` как read model;
- старый каталог как temporary storage model.

### Критерий завершения
Появляется доменная модель, которая может описать торрент без привязки к MonoTorrent и без флага `ShouldRun` как единственного правила.

---

## Этап 2. Ввести product-owned collection repository
### Проблема сейчас
Пользовательская коллекция торрентов и engine runtime слишком переплетены.

### Что делаем
Создаём отдельный контракт и реализацию:
- `ITorrentCollectionRepository`

Репозиторий должен хранить именно product model:
- список торрентов;
- их intent;
- metadata о выборе файлов;
- deferred actions;
- future per-torrent settings.

### Что это меняет
- каталог перестаёт быть просто кешем snapshot'ов;
- коллекция становится источником истины для продукта;
- startup больше не зависит от «что движок сумел восстановить».

### Критерий завершения
UI может получить коллекцию торрентов полностью из product repository до старта движка.

---

## Этап 3. Выделить engine gateway и убить идею центра в `MonoTorrentService`
### Проблема сейчас
Даже после распила старый сервис остаётся слишком важным узлом.

### Что делаем
Разделяем старый фасад на порты и адаптеры:
- `ITorrentEngineGateway`
- `ITorrentEngineLifecycle`
- `ITorrentRuntimeFactsProvider`
- `ITorrentEngineStateStore`

### Что удаляем как архитектурную идею
- один главный service, который знает всё про startup, add, restore, update, settings и notifications.

### Критерий завершения
MonoTorrent становится внешним движком за портом, а не ядром системы.

---

## Этап 4. Перенести startup/restore в application workflow
### Проблема сейчас
Startup/restore слишком близко к infrastructure.

### Что делаем
Создаём `RestoreTorrentCollectionWorkflow`, который:
1. загружает product-owned collection;
2. публикует раннюю read model;
3. запускает engine через gateway;
4. получает runtime facts;
5. через domain policies синхронизирует collection;
6. применяет intents и deferred actions;
7. публикует обновлённую read model.

### Что должно быть явно выражено
- кеш списка — нормальная часть продукта;
- runtime не уничтожает коллекцию;
- deferred actions применяются как часть workflow, а не side effect infrastructure startup.

### Критерий завершения
Startup читается как application-сценарий, а не как технический init движка.

---

## Этап 5. Перестроить команды вокруг domain model
### Проблема сейчас
Часть use cases уже есть, но они ещё опираются на старые abstraction patterns.

### Что делаем
Переходим к командам, которые работают с доменной моделью:
- `AddTorrentFileCommand`
- `AddMagnetCommand`
- `StartTorrentCommand`
- `PauseTorrentCommand`
- `RemoveTorrentCommand`
- `ApplyGlobalSettingsCommand`
- future `UpdatePerTorrentSettingsCommand`

Каждая команда:
- меняет domain model;
- обновляет repository;
- при необходимости инициирует вызов engine gateway;
- возвращает product-level result.

### Что важно
Duplicate, preview cancel, invalid magnet, engine unavailable и подобные случаи должны возвращаться как продуктовые результаты, а не как сырые исключения внешней библиотеки.

### Критерий завершения
Пользовательские команды выражаются в терминах домена и application-result, а не engine exceptions.

---

## Этап 6. Ввести явный deferred action механизм
### Проблема сейчас
Поддержка действий до старта движка уже есть, но она ещё слишком procedural.

### Что делаем
Создаём явную модель:
- deferred action queue как часть product repository или domain state;
- policy, определяющую, как схлопывать и применять действия;
- workflow применения после готовности engine.

### Что это даёт
- поведение из `USER_APP_LOGIC.md` становится частью системы;
- start/pause/remove до готовности engine больше не выглядят как особые обходные пути.

### Критерий завершения
Deferred actions становятся частью модели, а не особенностью startup-кода.

---

## Этап 7. Нормализовать read-side и projections
### Проблема сейчас
UI всё ещё слишком близко к snapshot/update инфраструктуре.

### Что делаем
Вводим нормальный read-side:
- `GetTorrentListQuery`
- `GetTorrentDetailsQuery`
- `TorrentListProjection`
- `TorrentDetailsProjection`
- `TorrentSpeedSummaryProjection`

Read model должна строиться из согласованной application/domain state, а не из сырых engine updates.

### Что убираем
- прямые подписки UI на engine-like snapshots;
- ручной merge cache/runtime в presentation;
- tray summary, считаемую в обход application read-side.

### Критерий завершения
UI получает read models, а не полуфабрикаты из infrastructure.

---

## Этап 8. Перестроить систему настроек
### Проблема сейчас
Настройки стали лучше, но пока ещё не выглядят как законченная расширяемая подсистема.

### Что делаем
Вводим три уровня:
- domain settings values + validation;
- application workflows `load/edit/apply/reset`;
- infrastructure settings repository + schema migration.

Разделяем:
- app settings;
- global torrent settings;
- per-torrent settings.

### Что важно
Рост настроек не должен вести к новым несогласованным UX-механикам.

### Критерий завершения
Новая настройка добавляется по одному шаблону, без прямых infrastructure-вызовов из ViewModel.

---

## Этап 9. Перестроить shell/close как policy-driven subsystem
### Проблема сейчас
Close-flow уже лучше, но всё ещё исторически связан с shell-слоем.

### Что делаем
Вводим:
- domain `AppCloseBehavior`;
- application workflows `HandleWindowClose` и `HandleTrayExit`;
- future `AskCloseBehavior` как расширение policy.

Shell adapters только исполняют:
- hide/show window;
- hide/show tray;
- exit.

### Критерий завершения
Возврат окна выбора при закрытии делается как развитие policy, а не как новая ветка в `App.xaml.cs`.

---

## Этап 10. Добавить torrent details как отдельный vertical slice
### Почему это важно
Это проверит зрелость новой архитектуры.

### Что делаем
Добавляем:
- `GetTorrentDetailsQuery`
- `OpenTorrentDetailsCommand`
- `TorrentDetailsProjection`
- future `UpdatePerTorrentSettingsCommand`

### Что проверяем
- UI details не лезет в infrastructure;
- список и детали не завязаны на один mutable UI-object;
- per-torrent settings ложатся в ту же модель, что и остальные настройки.

### Критерий завершения
Экран деталей добавлен как отдельный slice, а не как хаотичное расширение списка.

---

## Этап 11. Удалить transition-only код
### Проблема
Во время миграции неизбежно появятся мосты.

### Что делаем
Удаляем:
- старые snapshot-first фасады;
- bridge wrappers;
- устаревшие use cases;
- sync adapters между старым каталогом и новой моделью;
- всё, что отмечено как transition-only.

### Критерий завершения
В коде не остаётся permanent-temporary abstractions.

---

## Этап 12. Закрепить новую архитектуру тестами
### Что делаем
Добавляем тесты на:
- duplicate policy;
- restore policy;
- status resolver;
- deferred action policy;
- close behavior policy;
- settings apply workflows;
- torrent list/details projections.

Обновляем docs:
- `PROJECT_MAP.md`
- `ONBOARDING.md`
- `README_ARCHITECTURE.md`
- `TECH_DEBT_BACKLOG.md`

### Критерий завершения
Новая архитектура не только описана, но и защищена тестами и документацией.

---

## Рекомендуемый практический порядок
1. Этап 0 — зафиксировать продукт и запреты
2. Этап 1 — ввести новую доменную модель
3. Этап 2 — ввести product-owned repository
4. Этап 3 — убрать идею одного центра в `MonoTorrentService`
5. Этап 4 — перенести startup/restore в application workflow
6. Этап 5 — перестроить команды
7. Этап 6 — ввести deferred action механизм
8. Этап 7 — нормализовать read-side
9. Этап 8 — перестроить settings
10. Этап 9 — нормализовать shell/close
11. Этап 10 — добавить details slice
12. Этап 11 — удалить переходные мосты
13. Этап 12 — закрепить тестами

---

## Что может временно ломаться во время миграции
Допустимо, что на промежуточных этапах будут ломаться:
- старый update-flow;
- обратная совместимость старых внутренних моделей;
- старые use cases и DI wiring;
- старые snapshot assumptions.

Недопустимо терять как целевое правило:
- пользовательскую коллекцию торрентов;
- пользовательские намерения;
- возможность принимать действия до готовности движка;
- единообразную модель настроек.
