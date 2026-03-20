# NiTorrent — README_ARCHITECTURE

## Кратко
NiTorrent — desktop-приложение для работы с торрентами на WinUI 3 / .NET 10.
Проект уже разложен по слоям, близким к Clean Architecture, и это сильная сторона решения.

Основная задача архитектуры здесь — разделить:
- platform-specific запуск и UI
- presentation state
- сценарную логику
- инфраструктурную интеграцию с MonoTorrent
- доменные модели состояния

---

## Слои системы

```text
NiTorrent.App
 ├─> NiTorrent.Presentation
 │    └─> NiTorrent.Application
 │         └─> NiTorrent.Domain
 └─> NiTorrent.Infrastructure
      └─> NiTorrent.Application
           └─> NiTorrent.Domain
```

### Смысл слоёв

#### `NiTorrent.App`
Отвечает за:
- запуск приложения
- WinUI/XAML
- создание окна
- tray integration
- activation handling
- регистрацию platform-specific адаптеров
- composition root

Не должен содержать:
- сценарную бизнес-логику
- сложную orchestration-логику торрентов
- детали жизненного цикла торрент-движка

#### `NiTorrent.Presentation`
Отвечает за:
- ViewModel
- observable UI state
- команды уровня UI
- преобразование данных в вид, удобный для интерфейса

Не должен содержать:
- WinUI platform-specific детали
- прямую работу с файлами/процессами/MonoTorrent
- тяжёлую orchestration-логику

#### `NiTorrent.Application`
Отвечает за:
- интерфейсы между слоями
- use case / scenario orchestration
- application-level policies
- DTO и request/response модели

Сейчас слой тонкий, но именно сюда логично переносить:
- `AddTorrentUseCase`
- `PauseTorrentUseCase`
- `ResumeTorrentUseCase`
- `RemoveTorrentUseCase`
- другие сценарные обработчики

#### `NiTorrent.Domain`
Отвечает за:
- доменные типы
- инварианты идентификаторов и статусов
- модель состояния торрента

Слой компактный и в текущем состоянии выглядит самым чистым.

#### `NiTorrent.Infrastructure`
Отвечает за:
- интеграцию с MonoTorrent
- сохранение состояния
- фоновые процессы
- настройки
- каталог торрентов и persistence

Не должен брать на себя:
- UI concern'ы
- window/tray поведение
- presentation-specific mapping

---

## Текущая структура проекта

```text
src/
├─ NiTorrent.App/
├─ NiTorrent.Application/
├─ NiTorrent.Domain/
├─ NiTorrent.Infrastructure/
└─ NiTorrent.Presentation/
```

### `NiTorrent.App`
Ключевые элементы:
- `App.xaml.cs` — startup и composition root
- `MainWindow.xaml(.cs)` — главное окно
- `Views/*` — страницы и окна
- `Services/*` — WinUI/platform adapters

Примеры адаптеров:
- `WinPickerHelper`
- `WinUiDialogService`
- `WinUiDispatcher`
- `TrayService`
- `TorrentPreviewDialogService`

### `NiTorrent.Presentation`
Ключевые элементы:
- `Features/Torrents/TorrentViewModel.cs`
- `Features/Torrents/TorrentItemViewModel.cs`
- `Features/Torrents/TorrentPreviewViewModel.cs`
- `Features/Settings/*`
- `Features/Shell/MainViewModel.cs`

### `NiTorrent.Application`
Ключевые элементы:
- `Abstractions/ITorrentService.cs`
- `Abstractions/IAppPreferences.cs`
- `Abstractions/ITorrentPreferences.cs`
- `Abstractions/IDialogService.cs`
- `Torrents/AddTorrentRequest.cs`
- `Torrents/TorrentPreview.cs`
- `Torrents/TorrentSource.cs`

### `NiTorrent.Domain`
Ключевые элементы:
- `Torrents/TorrentId.cs`
- `Torrents/TorrentSnapshot.cs`
- `Torrents/TorrentStatus.cs`
- `Torrents/TorrentPhase.cs`

### `NiTorrent.Infrastructure`
Ключевые элементы:
- `Torrents/MonoTorrentService.cs`
- `Torrents/TorrentCatalogStore.cs`
- `Torrents/TorrentMonitor.cs`
- `Torrents/TorrentCommandQueue.cs`
- `Settings/JsonAppPreferences.cs`
- `Settings/JsonTorrentPreferences.cs`

---

## Основные потоки системы

## 1. Startup
Примерный текущий поток:
1. `App.xaml.cs` создаёт `Host`.
2. Регистрируются Presentation и Infrastructure.
3. Регистрируются WinUI/platform adapters.
4. Создаётся главное окно.
5. Инициализируется tray.
6. Стартует host.
7. Запускается инициализация торрент-движка.

### Архитектурное замечание
Сейчас этот поток слишком собран в `App.xaml.cs`. Это удобно на ранней стадии, но плохо масштабируется.

---

## 2. Activation `.torrent`
Примерный поток:
1. приложение получает activation
2. `App.xaml.cs` поднимает окно
3. через `ITorrentService` читается preview
4. через `ITorrentPreviewDialogService` пользователь выбирает файлы/папку
5. через `ITorrentService.AddAsync(...)` торрент добавляется

### Архитектурное замечание
Сценарий хороший по смыслу, но orchestration activation лучше держать в отдельном application/app service, а не прямо в `App.xaml.cs`.

---

## 3. Обновление состояния торрентов
Примерный поток:
1. `TorrentMonitor` каждые 2 секунды вызывает `ITorrentService.UpdateTorrent()`
2. сервис собирает snapshots
3. через событие обновляет подписчиков
4. `TorrentViewModel` синхронизирует UI

### Архитектурное замечание
Поток рабочий, но API обновления состояния выглядит смешанным: есть и pull, и push механизм. Это нормально на старте, но потом обычно создаёт лишнюю сложность.

---

## 4. Save / shutdown
Примерный поток:
1. закрытие окна переводит приложение в tray
2. при реальном выходе останавливается host
3. сохраняется состояние торрент-сервиса
4. закрывается окно
5. приложение завершает работу

### Архитектурное замечание
Shutdown path должен быть особенно простым и предсказуемым. Сейчас он требует дальнейшего упрощения и явной документации.

---

## Dependency Injection

### Infrastructure registration
`NiTorrent.Infrastructure` регистрирует:
- `ITorrentService -> MonoTorrentService`
- `IAppPreferences -> JsonAppPreferences`
- `ITorrentPreferences -> JsonTorrentPreferences`
- `TorrentCatalogStore`
- `TorrentMonitor` как hosted service

### Presentation registration
`NiTorrent.Presentation` регистрирует:
- `MainViewModel`
- `TorrentViewModel`
- `TorrentPreviewViewModel`
- settings view models

### App registration
`NiTorrent.App` добавляет platform adapters:
- `IUiDispatcher`
- `IDialogService`
- `IPickerHelper`
- `IUriLauncher`
- `ITrayService`
- `ITorrentPreviewDialogService`
- `IUpdateService`
- `IAppInfo`

---

## Архитектурные правила
Эти правила стоит считать обязательными.

### Правило 1
`Domain` не зависит ни от кого.

### Правило 2
`Application` может зависеть только от `Domain`.

### Правило 3
`Presentation` не должен знать про MonoTorrent, WinUI-specific implementation и файловую инфраструктуру.

### Правило 4
`Infrastructure` реализует интерфейсы `Application`, но не тянет в себя UI-зависимости.

### Правило 5
`App` — единственное место, где можно склеивать platform concerns и верхнеуровневый startup.

### Правило 6
Если в коде появляется orchestration сценария, первым кандидатом на размещение должен быть `Application`, а не `App` и не `Presentation`.

---

## Текущие сильные стороны архитектуры
- уже есть разделение на слои
- доменная модель компактная
- инфраструктура вынесена в отдельный проект
- platform-specific сервисы в целом отделены от Application
- есть явный `ITorrentService`, который можно удерживать как boundary

---

## Текущие проблемные точки

### `App.xaml.cs`
Слишком много startup и lifecycle orchestration.

### `MonoTorrentService`
Слишком широкий класс, фактически центр системы.

### `TorrentViewModel`
Слишком много coordination-логики для presentation-слоя.

### `ITorrentService`
Контракт со временем стал смешивать разные модели взаимодействия.

---

## Архитектурное направление на ближайший рефакторинг

### Куда двигаться
1. `App.xaml.cs` сделать тонким composition root.
2. Из `MonoTorrentService` сделать фасад над более мелкими компонентами.
3. Сценарии перенести в `Application`.
4. `Presentation` оставить слоем UI state и команд.
5. Сохранение состояния и recovery сделать отдельной, понятной политикой.

### Чего не делать
- не переписывать Domain без явной необходимости
- не пытаться сразу заменить все события на новую реактивную модель
- не переносить platform-specific код в Application “ради удобства”
- не трогать структуру папок без конкретной цели

---

## Рекомендуемые будущие папки / компоненты
Это не обязательная финальная схема, а удобное направление.

### В `NiTorrent.Application`
```text
Application/
├─ Abstractions/
├─ Torrents/
│  ├─ Commands/
│  ├─ Queries/
│  ├─ UseCases/
│  └─ Policies/
```

### В `NiTorrent.Infrastructure/Torrents`
```text
Torrents/
├─ Engine/
├─ Recovery/
├─ Snapshots/
├─ Persistence/
├─ Commands/
└─ Monitoring/
```

### В `NiTorrent.App`
```text
App/
├─ Startup/
├─ Activation/
├─ Windowing/
└─ Services/
```

---

## Для нового разработчика
Если нужно быстро понять проект, идти лучше так:

1. прочитать этот файл
2. открыть `docs/project-map/PROJECT_MAP.md`
3. посмотреть `App.xaml.cs`
4. посмотреть `ITorrentService`
5. посмотреть `MonoTorrentService`
6. посмотреть `TorrentViewModel`
7. только потом идти в XAML и settings

Так быстрее складывается полная картина.

---

## Короткий вывод
Архитектура у проекта уже не плохая: основная проблема не в отсутствии слоёв, а в том, что несколько центральных классов начали тянуть слишком много ответственности.

Значит рефакторинг стоит делать не “с нуля”, а через:
- сохранение существующих границ
- декомпозицию hot spots
- перенос orchestration в `Application`
- упрощение startup и shutdown
