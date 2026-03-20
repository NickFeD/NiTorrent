# NiTorrent — карта проекта

## Что это за проект
NiTorrent — desktop-приложение для работы с торрентами на **WinUI 3 / .NET 10**, собранное по слоям, близким к Clean Architecture:

- **NiTorrent.App** — точка входа, WinUI/XAML, platform-specific сервисы
- **NiTorrent.Presentation** — ViewModel-слой и UI-логика
- **NiTorrent.Application** — контракты и DTO/use-case boundary
- **NiTorrent.Domain** — доменные модели и enum'ы
- **NiTorrent.Infrastructure** — реализация торрент-движка, хранение настроек, persistence

Базовый поток зависимостей выглядит так:

```text
NiTorrent.App
 ├─> NiTorrent.Presentation
 │    └─> NiTorrent.Application
 │         └─> NiTorrent.Domain
 └─> NiTorrent.Infrastructure
      └─> NiTorrent.Application
           └─> NiTorrent.Domain
```

---

## Структура решения

```text
NiTorrent/
├─ src/
│  ├─ NiTorrent.App/
│  ├─ NiTorrent.Application/
│  ├─ NiTorrent.Domain/
│  ├─ NiTorrent.Infrastructure/
│  └─ NiTorrent.Presentation/
├─ NiTorrent.slnx
└─ docs/
   └─ project-map/
      └─ PROJECT_MAP.md
```

> Технические каталоги `.git`, `.vs`, `bin`, `obj` в карту не включаются как не несущие бизнес-структуру.

---

## 1. NiTorrent.App

### Назначение
Внешняя оболочка приложения:
- запуск приложения
- настройка DI/Host
- инициализация окна
- tray integration
- file activation для `.torrent`
- WinUI-specific сервисы

### Главные файлы
- `src/NiTorrent.App/App.xaml.cs` — главный composition root, запуск host, wiring сервисов, обработка single-instance и file activation
- `src/NiTorrent.App/MainWindow.xaml` / `.cs` — главное окно
- `src/NiTorrent.App/Views/*` — страницы и окна WinUI
- `src/NiTorrent.App/Services/*` — platform-specific адаптеры для интерфейсов из Application/Presentation

### Важные сервисы
- `AppStorageService` — доступ к локальному хранению
- `TrayService` — работа с треем
- `WinPickerHelper` — file/folder picker
- `WinUiDialogService` — диалоги
- `WinUiDispatcher` / `UiDispatcherHolder` — маршалинг в UI thread
- `TorrentPreviewDialogService` — предпросмотр торрента перед добавлением

### Особенности
`App.xaml.cs` сейчас содержит сразу несколько ролей:
- startup orchestration
- DI registration
- window lifecycle
- activation handling
- shutdown logic
- bootstrap torrent engine

Это один из первых кандидатов на декомпозицию.

---

## 2. NiTorrent.Presentation

### Назначение
Слой UI-моделей и presentation logic без привязки к WinUI-конкретике.

### Структура

```text
NiTorrent.Presentation/
├─ Abstractions/
│  ├─ ITorrentPreviewDialogService.cs
│  ├─ ITrayService.cs
│  └─ IUiDispatcher.cs
├─ Features/
│  ├─ Settings/
│  ├─ Shell/
│  └─ Torrents/
├─ DependencyInjection.cs
└─ SizeFormatter.cs
```

### Главные ViewModel
- `Features/Shell/MainViewModel.cs` — shell-уровень, пока почти пустой
- `Features/Torrents/TorrentViewModel.cs` — центральная VM списка торрентов и команд
- `Features/Torrents/TorrentItemViewModel.cs` — состояние одного торрента
- `Features/Torrents/TorrentPreviewViewModel.cs` — предпросмотр содержимого торрента
- `Features/Settings/TorrentSettingsViewModel.cs` — экран настроек торрента
- `Features/Settings/AppUpdateSettingViewModel.cs`
- `Features/Settings/AboutUsSettingViewModel.cs`

### Подмодуль дерева файлов

```text
Features/Torrents/Tree/
├─ FileNode.cs
├─ FolderModel.cs
├─ TorrentTreeItemViewModel.cs
└─ TorrentTreeModel.cs
```

Используется для выбора файлов внутри торрента перед загрузкой.

### Наблюдения
Самая насыщенная точка слоя — `TorrentViewModel.cs`.
Сейчас она совмещает:
- подписку на события сервиса
- синхронизацию коллекции UI
- агрегацию скоростей
- orchestration add/remove/start/pause
- работу с файловой системой (`Process.Start`)

Это главный кандидат на выделение use-case oriented presenter/command handlers.

---

## 3. NiTorrent.Application

### Назначение
Контракты между UI и реализациями инфраструктуры.

### Структура

```text
NiTorrent.Application/
├─ Abstractions/
│  ├─ IAppInfo.cs
│  ├─ IAppPreferences.cs
│  ├─ IAppStorageService.cs
│  ├─ IDialogService.cs
│  ├─ IPickerHelper.cs
│  ├─ ITorrentPreferences.cs
│  ├─ ITorrentService.cs
│  ├─ IUpdateService.cs
│  └─ IUriLauncher.cs
└─ Torrents/
   ├─ AddTorrentRequest.cs
   ├─ TorrentFileEntry.cs
   ├─ TorrentPreview.cs
   └─ TorrentSource.cs
```

### Что здесь важно
Слой пока очень тонкий: здесь почти нет use case классов, только интерфейсы и модели обмена.

Это нормально для старта, но при росте проекта именно сюда логично переносить:
- orchestration сценариев
- команды/handlers
- правила взаимодействия Presentation ↔ Infrastructure

Иначе Presentation и App будут разрастаться.

---

## 4. NiTorrent.Domain

### Назначение
Минимальное доменное ядро.

### Структура

```text
NiTorrent.Domain/
├─ Settings/
│  └─ TorrentFastResumeMode.cs
└─ Torrents/
   ├─ TorrentId.cs
   ├─ TorrentPhase.cs
   ├─ TorrentSnapshot.cs
   └─ TorrentStatus.cs
```

### Ключевые сущности
- `TorrentId` — стабильный доменный идентификатор
- `TorrentSnapshot` — срез состояния торрента для UI/сервисов
- `TorrentStatus` — текущая стадия, прогресс, скорости
- `TorrentPhase` — enum фаз

### Наблюдения
Domain пока компактный и достаточно чистый.
Это хороший знак: рефакторинг лучше начинать не с Domain, а с orchestration-слоёв выше.

---

## 5. NiTorrent.Infrastructure

### Назначение
Интеграция с MonoTorrent, сохранение состояния, настройки и фоновый мониторинг.

### Структура

```text
NiTorrent.Infrastructure/
├─ DI/
│  └─ DependencyInjection.cs
├─ Settings/
│  ├─ AppConfig.cs
│  ├─ AppConfigLoader.cs
│  ├─ JsonAppPreferences.cs
│  ├─ JsonTorrentPreferences.cs
│  ├─ TorrentConfig.cs
│  └─ TorrentConfigLoader.cs
├─ Torrents/
│  ├─ MonoTorrentService.cs
│  ├─ TorrentCatalog.cs
│  ├─ TorrentCatalogStore.cs
│  ├─ TorrentCommandQueue.cs
│  └─ TorrentMonitor.cs
└─ InfrastructurePaths.cs
```

### Главные компоненты
- `MonoTorrentService` — основная реализация `ITorrentService`
- `TorrentCatalogStore` — хранение метаданных и намерения запуска (`ShouldRun`)
- `TorrentMonitor` — фоновое обновление состояния
- `TorrentCommandQueue` — очередь намерений до готовности движка
- `JsonAppPreferences` / `JsonTorrentPreferences` — сохранение настроек

### Что важно архитектурно
`MonoTorrentService` сейчас — фактическое ядро приложения. В нём смешаны:
- lifecycle движка
- восстановление состояния
- добавление/старт/пауза/удаление
- построение snapshot
- координация с catalog
- фоновые операции и async orchestration
- синхронизация через `SemaphoreSlim`

Это ключевая hot spot зона для багов, деградации производительности и сложности онбординга.

---

## Основные сценарии работы

### 1. Запуск приложения
1. `NiTorrent.App/App.xaml.cs` создаёт host и регистрирует сервисы
2. Подключаются `AddNiTorrentInfrastructure()` и `AddNiTorrentPresentation()`
3. Создаётся главное окно
4. Инициализируется трей
5. Стартует torrent engine через `ITorrentService.InitializeAsync()`

### 2. Добавление `.torrent` файла из UI
1. `TorrentViewModel.PickTorrent()` вызывает `IPickerHelper`
2. `ITorrentService.GetPreviewAsync()` получает preview
3. `ITorrentPreviewDialogService` показывает окно выбора файлов
4. `ITorrentService.AddAsync()` добавляет торрент и сохраняет состояние

### 3. Открытие `.torrent` файла через shell activation
1. `App.HandleActivationAsync()` получает файл
2. Показывает главное окно
3. Получает preview через `ITorrentService`
4. Открывает preview dialog
5. Добавляет торрент

### 4. Обновление списка торрентов
1. Infrastructure публикует snapshots
2. `TorrentViewModel` получает событие
3. Через `IUiDispatcher` обновляет `ObservableCollection`
4. UI перерисовывает список и скорости

---

## Карта зависимостей по ответственности

### UI / platform
- `NiTorrent.App`
- XAML pages, window, tray, picker, dialogs

### Presentation / state for screens
- `NiTorrent.Presentation`
- ViewModel, tree-model, UI commands

### Contracts / boundary
- `NiTorrent.Application`
- интерфейсы и DTO

### Business state
- `NiTorrent.Domain`
- id, status, snapshot, enum'ы

### Engine / persistence / IO
- `NiTorrent.Infrastructure`
- MonoTorrent, config, file storage, monitor

---

## Зоны повышенного риска

### 1. `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`
Причины:
- очень широкая ответственность
- высокая конкуренция async/state logic
- много побочных эффектов
- это центральная точка для багов и нестабильных релизов

### 2. `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`
Причины:
- слишком много orchestration-логики в VM
- напрямую управляет сценариями приложения
- смешивает UI-state, business-flow и системные действия

### 3. `src/NiTorrent.App/App.xaml.cs`
Причины:
- перегруженный composition root
- lifecycle + startup + activation + shutdown в одном месте

### 4. Settings persistence
Файлы:
- `JsonAppPreferences.cs`
- `JsonTorrentPreferences.cs`
- `AppConfigLoader.cs`
- `TorrentConfigLoader.cs`

Причины:
- важно проверить консистентность форматов и поведение при повреждённых конфигах

---

## Что уже выглядит удачно

- есть явное разделение по проектам
- Domain и Application пока не перегружены
- зависимости направлены в целом правильно
- инфраструктура подключается через DI extension
- UI вынесен из engine-кода

Это значит, что проект лучше **эволюционно рефакторить**, а не переписывать.

---

## Рекомендуемый порядок рефакторинга именно для этого проекта

### Этап 1. Зафиксировать карту и правила слоёв
Сначала договориться:
- `App` не хранит бизнес-flow, только composition/startup/platform wiring
- `Presentation` не должен напрямую знать о platform-specific деталях кроме абстракций
- orchestration сценариев постепенно переносится в `Application`
- `Infrastructure` не должен становиться вторым application-layer

### Этап 2. Разгрузить `App.xaml.cs`
Первое безопасное упрощение:
- вынести `ConfigureServices` в отдельный bootstrapper
- вынести activation handling в отдельный service
- вынести startup/shutdown coordination в app lifecycle service

Почему это хороший первый шаг:
- уменьшает когнитивную нагрузку
- почти не требует переписывать доменную логику
- улучшает онбординг

### Этап 3. Разрезать `MonoTorrentService`
Не переписывать сразу целиком, а выделить части:
- `TorrentEngineLifecycle`
- `TorrentSnapshotFactory`
- `TorrentSessionCoordinator`
- `TorrentStatePersistence`
- `TorrentCommandApplier`

Начинать с **выделения чтения/сохранения state** и **построения snapshot**, потому что это обычно проще и безопаснее, чем трогать runtime orchestration.

### Этап 4. Упростить `TorrentViewModel`
Постепенно вынести из неё:
- add/start/pause/remove сценарии в application/use-case слой
- open-folder в отдельный abstraction/service
- aggregation логики списка в отдельный presenter/state adapter

Цель: сделать `TorrentViewModel` в основном координатором экранного состояния, а не центром бизнес-команд.

### Этап 5. Ввести use-case классы в `NiTorrent.Application`
Например:
- `AddTorrentUseCase`
- `StartTorrentUseCase`
- `PauseTorrentUseCase`
- `RemoveTorrentUseCase`
- `GetTorrentPreviewUseCase`
- `ApplyTorrentSettingsUseCase`

Это уменьшит связность между Presentation и Infrastructure.

### Этап 6. Отдельно разобрать обновление состояния
Проверить и при необходимости реорганизовать связку:
- `TorrentMonitor`
- события `Loaded` / `UptateTorrent`
- UI dispatcher
- обновление `ObservableCollection`

Тут вероятны источники:
- лишних перерисовок
- гонок состояний
- сложных для воспроизведения багов

### Этап 7. После этого идти в производительность
Только после упрощения структуры анализировать:
- частоту публикации snapshots
- размер и стоимость пересборки коллекции
- количество UI update операций
- операции Save/Restore
- лишние фоновые задачи

---

## Минимальный backlog для следующего шага

### Документация
- [ ] добавить `README_ARCHITECTURE.md` с правилами слоёв
- [ ] зафиксировать allowed dependencies между проектами

### Безопасность изменений
- [ ] добавить smoke-тесты на add/start/pause/remove
- [ ] добавить сценарий инициализации с восстановлением состояния
- [ ] проверить поведение при битом конфиге/каталоге

### Декомпозиция
- [ ] вынести startup orchestration из `App.xaml.cs`
- [ ] выделить части `MonoTorrentService`
- [ ] сократить ответственность `TorrentViewModel`

---

## Быстрый вывод

Если цель — **снизить баги, ускорить онбординг, улучшить производительность и стабилизировать релизы**, то стартовать лучше в таком порядке:

1. **App.xaml.cs** — разгрузить startup/activation
2. **MonoTorrentService** — разделить по ответственности
3. **TorrentViewModel** — убрать из VM orchestration
4. **Application layer** — добавить use-case классы
5. **Monitor/update pipeline** — стабилизировать поток обновлений
6. только потом заниматься локальной полировкой и косметикой

---

## Где искать дальше

### Точки входа
- `src/NiTorrent.App/App.xaml.cs`
- `src/NiTorrent.App/MainWindow.xaml.cs`

### Главная бизнес-нагрузка
- `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`
- `src/NiTorrent.Infrastructure/Torrents/TorrentCatalogStore.cs`
- `src/NiTorrent.Infrastructure/Torrents/TorrentMonitor.cs`

### Главная UI-координация
- `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`
- `src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs`
- `src/NiTorrent.Presentation/Features/Torrents/TorrentPreviewViewModel.cs`

### Настройки
- `src/NiTorrent.Presentation/Features/Settings/TorrentSettingsViewModel.cs`
- `src/NiTorrent.Infrastructure/Settings/*`

