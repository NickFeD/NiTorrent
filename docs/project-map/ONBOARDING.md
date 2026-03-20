# NiTorrent — ONBOARDING

## Для кого этот документ
Этот документ нужен разработчику, который впервые открыл проект и хочет быстро понять:
- как собрать и запустить приложение
- в каком порядке читать код
- где находятся основные сценарии
- куда вносить изменения, а куда лучше не лезть первым PR

Главная цель онбординга здесь — **не пытаться понять весь проект сразу**.
Сначала нужно собрать рабочую ментальную модель, потом уже заходить в hotspot'ы.

---

## 1. Что это за проект
NiTorrent — desktop-приложение для работы с торрентами на **WinUI 3 / .NET 10**.

Проект разделён на 5 основных слоёв:

```text
NiTorrent.App
 ├─> NiTorrent.Presentation
 │    └─> NiTorrent.Application
 │         └─> NiTorrent.Domain
 └─> NiTorrent.Infrastructure
      └─> NiTorrent.Application
           └─> NiTorrent.Domain
```

### Очень коротко по слоям
- **NiTorrent.App** — запуск приложения, окно, tray, WinUI-specific адаптеры
- **NiTorrent.Presentation** — ViewModel и presentation state
- **NiTorrent.Application** — интерфейсы и будущие use case / handlers
- **NiTorrent.Domain** — доменные модели состояний торрентов
- **NiTorrent.Infrastructure** — MonoTorrent, persistence, настройки, фоновые процессы

Если запомнить только одно правило, то оно такое:

> UI и platform-specific код не должны протекать вниз, а orchestration не должна жить в `App.xaml.cs` и разрастаться во ViewModel.

---

## 2. Что прочитать в первую очередь
Не ходить по проекту хаотично. Читай в этом порядке.

### Шаг 1. Общая карта
1. `docs/project-map/PROJECT_MAP.md`
2. `docs/project-map/README_ARCHITECTURE.md`
3. `docs/project-map/REFORM_PLAN.md`

Это даст картину уровней, зависимостей и текущих проблем проекта.

### Шаг 2. Точка входа
4. `src/NiTorrent.App/App.xaml.cs`
5. `src/NiTorrent.App/MainWindow.xaml.cs`

На этом этапе задача не в том, чтобы понять каждую строчку, а в том, чтобы увидеть:
- как поднимается host
- где регистрируются сервисы
- как создаётся окно
- где начинается startup/activation flow

### Шаг 3. Основной UI-поток
6. `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`
7. `src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs`
8. `src/NiTorrent.Presentation/Features/Torrents/TorrentPreviewViewModel.cs`

Это покажет, как UI взаимодействует с `ITorrentService`.

### Шаг 4. Контракты
9. `src/NiTorrent.Application/Abstractions/ITorrentService.cs`
10. остальные интерфейсы в `src/NiTorrent.Application/Abstractions/`

Здесь фиксируются границы между слоями. Это важнее, чем сразу читать инфраструктурную реализацию.

### Шаг 5. Реальная инфраструктура
11. `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`
12. `src/NiTorrent.Infrastructure/Torrents/TorrentMonitor.cs`
13. `src/NiTorrent.Infrastructure/Torrents/TorrentCatalogStore.cs`
14. `src/NiTorrent.Infrastructure/Torrents/TorrentCommandQueue.cs`

Только после этого есть смысл разбираться в деталях торрент-ядра.

---

## 3. Как читать проект без перегруза
Хороший способ чтения:

### Сначала понять 4 сквозных сценария
- запуск приложения
- открытие `.torrent` файла
- добавление торрента через preview
- pause / start / remove / restore state

Если ты понимаешь эти 4 потока, то уже понимаешь большую часть системы.

### Не читать проект "по папкам"
Нужно читать **по сценариям**, а не просто сверху вниз.

Например, для сценария `Add torrent` идти так:
1. UI команда во ViewModel
2. вызов `ITorrentService`
3. реализация в `MonoTorrentService`
4. обновление snapshot / события
5. обратное попадание данных в ViewModel

Такой способ быстрее создаёт цельную картину.

---

## 4. Быстрый маршрут по ключевым сценариям

## Сценарий A. Startup
Смотреть:
- `src/NiTorrent.App/App.xaml.cs`
- `src/NiTorrent.Infrastructure/DI/DependencyInjection.cs`
- `src/NiTorrent.Presentation/DependencyInjection.cs`
- `src/NiTorrent.Infrastructure/Torrents/TorrentMonitor.cs`

Что понять:
- как строится DI
- какие сервисы singleton/scoped/hosted
- где стартует мониторинг торрентов
- как приложение поднимает главное окно

## Сценарий B. File activation `.torrent`
Смотреть:
- `App.xaml.cs`
- `TorrentPreviewDialogService`
- `ITorrentService`
- `MonoTorrentService`

Что понять:
- как приложение получает путь к `.torrent`
- как строится preview
- где пользователь выбирает файлы и папку
- где происходит фактическое добавление

## Сценарий C. Обновление списка торрентов
Смотреть:
- `TorrentMonitor.cs`
- `MonoTorrentService.cs`
- `TorrentViewModel.cs`
- `TorrentItemViewModel.cs`

Что понять:
- откуда приходит обновление состояния
- как строятся snapshot'ы
- как UI синхронизирует коллекцию
- где считается агрегированная статистика

## Сценарий D. Shutdown / Save / Tray
Смотреть:
- `App.xaml.cs`
- `TrayService.cs`
- `AppStorageService.cs`
- `MonoTorrentService.cs`

Что понять:
- как работает hide-to-tray
- когда приложение реально завершается
- где сохраняется состояние
- какие есть риски для регрессии при выходе

---

## 5. Что в проекте считается hot spot
Новому разработчику лучше заранее знать, где код наиболее чувствителен.

### 1. `src/NiTorrent.App/App.xaml.cs`
Это не просто точка входа, а пока ещё и startup coordinator.

Туда не стоит добавлять новую сложную бизнес-логику.
Лучше выносить её в отдельные сервисы.

### 2. `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`
Это центральная ViewModel и одна из самых загруженных точек.

Любые изменения здесь могут затронуть:
- UI state
- orchestration
- команды
- подписки
- обновление коллекций

Нужны маленькие PR и аккуратная проверка.

### 3. `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`
Самый опасный участок для регрессий.

Без необходимости не стоит сразу делать в нём большой рефакторинг. Сначала нужно понять текущий flow и зафиксировать поведение через чеклист.

---

## 6. Что делать в первый рабочий день
Рекомендуемый план для нового участника:

### Первые 30–40 минут
- прочитать `PROJECT_MAP.md`
- прочитать `README_ARCHITECTURE.md`
- просмотреть `REFORM_PLAN.md`
- открыть решение и увидеть список проектов

### Следующий час
- пройти `App.xaml.cs`
- пройти `TorrentViewModel.cs`
- посмотреть `ITorrentService.cs`
- найти реализацию `MonoTorrentService.cs`

### После этого
- пройти ручной smoke из `REGRESSION_CHECKLIST.md`
- попробовать проследить один сценарий end-to-end
- выбрать маленькую безопасную задачу

### Хорошая первая задача
- улучшить логирование
- выделить маленький helper
- поправить naming/читабельность без изменения поведения
- задокументировать sequence одного сценария

### Плохая первая задача
- переписывать `MonoTorrentService` целиком
- менять startup/shutdown поведение без чеклиста
- совмещать UI-изменения и архитектурный перенос в одном PR

---

## 7. Куда класть новый код

### Если это WinUI / окно / tray / picker / dialog
Класть в:
- `NiTorrent.App`

### Если это ViewModel / UI state / команды уровня интерфейса
Класть в:
- `NiTorrent.Presentation`

### Если это сценарий типа add / pause / remove / restore
Класть в:
- `NiTorrent.Application`

Даже если сейчас слой тонкий, именно сюда проект должен эволюционировать.

### Если это доменный тип, статус, идентификатор, enum
Класть в:
- `NiTorrent.Domain`

### Если это MonoTorrent, настройки, хранение, каталог, persistence
Класть в:
- `NiTorrent.Infrastructure`

---

## 8. Чего лучше не делать

### Не добавлять новую orchestration-логику в `App.xaml.cs`
Точка входа уже перегружена.

### Не тащить platform-specific код в Presentation
Например:
- прямую работу с WinUI controls
- file picker детали
- tray/window поведение

### Не делать `MonoTorrentService` ещё умнее
Если нужно добавить новый сценарий, лучше думать, какой кусок можно вынести, а не добавлять ещё один большой метод.

### Не смешивать в одном PR
- функциональные изменения
- рефакторинг
- переименование
- перенос файлов

Иначе ревью станет тяжелее, а риск регрессий — выше.

---

## 9. Как делать первый PR безопасно

### Минимальные правила
1. Один PR — одна цель.
2. Не менять поведение и структуру одновременно.
3. Пройти `REGRESSION_CHECKLIST.md` хотя бы по затронутому сценарию.
4. Если правка касается startup / shutdown / restore / add torrent — проверять руками обязательно.
5. Если в процессе нашёл более крупную проблему — зафиксировать её в `TECH_DEBT_BACKLOG.md`, а не пытаться решить всё сразу.

---

## 10. Быстрая ментальная модель системы
Упрощённо проект можно держать в голове так:

```text
WinUI/App
  -> ViewModel
    -> Application contracts / use cases
      -> Infrastructure services
        -> MonoTorrent runtime + local persistence
  <- snapshots / events / observable state <-
```

То есть:
- сверху пользовательские действия
- внизу реальный торрент-движок и сохранение
- между ними контракты и orchestration
- обратно вверх идут состояния и обновления для UI

---

## 11. Что открыть, если нужно быстро решить конкретную задачу

### Нужно понять, почему приложение странно стартует
Открывай:
- `App.xaml.cs`
- `DependencyInjection.cs`
- `TorrentMonitor.cs`

### Нужно понять, почему не обновляется UI
Открывай:
- `TorrentViewModel.cs`
- `TorrentItemViewModel.cs`
- `MonoTorrentService.cs`
- `TorrentMonitor.cs`

### Нужно понять, где сохраняются настройки
Открывай:
- `JsonAppPreferences.cs`
- `JsonTorrentPreferences.cs`
- `AppConfigLoader.cs`
- `TorrentConfigLoader.cs`

### Нужно понять, как работает preview перед добавлением
Открывай:
- `TorrentPreviewDialogService.cs`
- `TorrentPreviewViewModel.cs`
- `ITorrentService.cs`
- `MonoTorrentService.cs`

---

## 12. Что стоит сделать следующим документом
После этого onboarding логично будет поддерживать ещё 2 типа материалов:
- короткие sequence-описания для сценариев `Add torrent`, `Restore on startup`, `Shutdown`
- `ANTI_PATTERNS.md` с примерами, что не надо добавлять в `App`, `Presentation` и `Infrastructure`

---

## Итог
Если ты новый разработчик в этом проекте, не пытайся сразу понять весь код.

Рабочий порядок такой:
1. прочитать карту и архитектуру
2. понять startup и основной UI flow
3. пройти один сценарий end-to-end
4. не начинать с крупнейших hotspot'ов
5. делать первый PR маленьким и безопасным

Этого уже достаточно, чтобы начать вносить изменения без погружения в хаос.
