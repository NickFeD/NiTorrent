# NiTorrent — TARGET_ARCHITECTURE

## Зачем нужен этот документ
Этот документ фиксирует **целевую архитектуру** проекта не в абстрактном виде, а с учётом:
- текущего состояния кода
- уже работающих решений, которые не стоит ломать без необходимости
- анти-паттернов из `ANTI_PATTERNS.md`
- запланированного развития продукта

Документ нужен как ориентир для следующих этапов рефакторинга:
- чтобы понимать, **что сохраняем** из текущего проекта
- что считаем **временным компромиссом**
- какие правила должны определять размещение нового кода
- как не размыть архитектуру, когда появятся новые настройки, экран подробностей торрента и более богатые shell-сценарии

Этот файл **не описывает текущую реализацию построчно**.
Он описывает **куда проект должен прийти**, при этом используя сильные стороны уже существующей структуры.

---

## Исходные ограничения и принципы

### 1. Опора на реальный проект, а не на «идеальную картинку»
NiTorrent уже имеет полезную слоистую структуру:

```text
NiTorrent.App
 ├─> NiTorrent.Presentation
 │    └─> NiTorrent.Application
 │         └─> NiTorrent.Domain
 └─> NiTorrent.Infrastructure
      └─> NiTorrent.Application
           └─> NiTorrent.Domain
```

Это нужно **сохранить**, а не ломать ради чистой теории.

### 2. Clean Architecture как направление, а не догма
В идеале бизнес-правила стремятся в `Domain` и `Application`.
Но проект уже сильно завязан на MonoTorrent и WinUI, поэтому перенос должен быть:
- поэтапным
- безопасным
- без регрессий рабочего билда

### 3. Анти-паттерны обязательны к соблюдению
При любом новом изменении нельзя создавать заново:
- god class
- толстую ViewModel
- composition root с бизнес-координацией
- протечку platform-specific кода выше `App`
- смешение push/pull модели без явных правил
- скрытые side effects
- скрытые temporal coupling зависимости

Если новый код делает одно из этого — он размещён неправильно, даже если “работает”.

---

## Продуктовые вводные, которые архитектура обязана выдержать

Целевая архитектура должна уже сейчас учитывать, что в проекте будут развиваться следующие зоны:

### 1. Настройки будут расширяться
Текущие настройки — это не предел.
Будут появляться:
- новые глобальные настройки приложения
- новые настройки torrent engine
- shell-настройки
- поведение при закрытии
- possibly интеграционные и diagnostic настройки

Следствие для архитектуры:
- настройки нельзя держать как разрозненные toggle-и с разной логикой сохранения
- нужна единая модель: `load -> edit -> validate -> apply -> persist`
- настройки должны быть расширяемыми без разрастания `App.xaml.cs` и без copy-paste ViewModel

### 2. Появится экран подробностей торрента
Планируется, что по двойному клику по элементу списка будет открываться экран/страница подробностей.
Вероятно там появятся:
- сведения о торренте
- файлы
- трекеры
- статистика
- возможно настройки конкретного торрента

Следствие для архитектуры:
- текущий список торрентов нельзя считать конечным UI
- нужен явный слой `Torrent Details` в Presentation/Application
- текущие snapshots должны быть пригодны не только для списка, но и для детального представления
- per-torrent settings нельзя пришивать прямо к `TorrentItemViewModel`

### 3. Вернётся окно выбора при закрытии
Окно выбора действия при закрытии — это будущая feature.
Но после рефакторинга оно должно вернуться:
- без возврата логики в `App.xaml.cs`
- без разрастания tray/close-flow
- как расширение существующей close-policy системы

Следствие:
- close logic должна остаться policy-driven
- UI выбора действия должен быть отдельным адаптером поверх `Application`/`App close coordinator`

---

## Главное архитектурное решение

### Архитектура проекта должна быть scenario-centered
Не “по папкам” и не “по слоям ради слоёв”, а вокруг сценариев пользователя.

Основная формула:

- `Domain` — правила предметной области и инварианты
- `Application` — сценарии и orchestration бизнес-решений
- `Infrastructure` — техническая реализация engine, persistence, filesystem, settings storage
- `Presentation` — состояние экрана и команды UI
- `App` — WinUI shell, окно, tray, navigation, activation, platform adapters

То есть **центр архитектуры — не MonoTorrent и не WinUI, а сценарии пользователя**.

---

# Целевая модель слоёв

## 1. Domain

### Назначение
Содержит устойчивые правила предметной области торрент-приложения, которые не должны зависеть от:
- MonoTorrent
- WinUI
- JSON storage
- конкретной ViewModel

### Что должно жить в Domain

#### 1. Идентичность и инварианты торрента
- `TorrentId`
- `TorrentKey` / stable identity для дедупликации
- правила сравнения и идентификации одного и того же торрента

#### 2. Состояния и переходы
- `TorrentPhase`
- доменные правила допустимых переходов:
  - что значит “paused”
  - что значит “running intent”
  - что значит “restored but engine not ready yet”

#### 3. Намерение пользователя относительно торрента
Сейчас это фактически живёт через `ShouldRun` и очереди команд до старта движка.
Целевое место для этого знания — ближе к Domain/Application, а не в глубине infrastructure.

Нужны доменные понятия вроде:
- `DesiredTorrentState`
- `TorrentRunIntent`
- `TorrentLifecycleDecision`

#### 4. Доменные модели настроек
Не сами JSON-конфиги, а устойчивые модели и инварианты:
- допустимые диапазоны скоростей
- режимы fast resume
- правила портов/авто-настроек
- пер-torrent ограничения, когда они появятся

### Чего не должно быть в Domain
- MonoTorrent types
- storage DTO
- WinUI/XAML types
- dispatcher/window/tray
- dialog/picker implementations

### Почему Domain пока ещё тонкий
Это нормально для текущей стадии проекта.
Не нужно насильно перетаскивать всё сразу.
Но новые **правила** нужно по возможности складывать именно сюда, а не усиливать зависимость от infrastructure.

---

## 2. Application

### Назначение
Это главный слой сценариев системы.
Если сформулировать очень коротко:

> `Application` отвечает на вопрос “что должно произойти для пользователя и в каком порядке”.

### Что должно жить в Application

#### 1. Use cases / workflows
Примеры:
- Add torrent from file
- Add torrent from magnet
- Preview before add
- Start torrent
- Pause torrent
- Remove torrent
- Open torrent folder
- Apply torrent settings
- Navigate to torrent details
- Save per-torrent settings

#### 2. Application policies
Например:
- как применять сохранённое намерение запуска после старта движка
- когда показывать cached snapshots, а когда runtime state
- как трактовать duplicate add
- как работать с закрытием окна: hide / exit / ask

#### 3. DTO / request / response модели сценариев
Например:
- `AddTorrentRequest`
- `TorrentPreview`
- `TorrentDetailsDto`
- `TorrentSettingsDto`
- `CloseAppDecision`

#### 4. Порты (интерфейсы)
Например:
- `ITorrentService` или более узкие контракты по сценариям
- `ITorrentWorkflowService`
- `ITorrentDetailsService`
- `ISettingsWorkflowService`
- `IAppClosePolicyService`
- `IDialogService`, `IPickerHelper`, `IUriLauncher`, `IFolderLauncher`

### Что не должно жить в Application
- `Window`, `AppWindow`, `DispatcherQueue`, `FileOpenPicker`
- `TorrentManager`, `ClientEngine`
- JSON files и их конкретная сериализация
- XAML navigation implementation

### Целевая форма Application
Слой должен стать **центром системы**, а не только папкой с интерфейсами.

Минимальные application-подсистемы на будущее:

```text
Application/
├─ Torrents/
│  ├─ Workflows/
│  ├─ Commands/
│  ├─ Queries/
│  ├─ Details/
│  └─ Settings/
├─ Shell/
│  ├─ Close/
│  ├─ Activation/
│  └─ Navigation/
└─ Settings/
   ├─ Global/
   └─ TorrentEngine/
```

### Главное правило размещения
Если появляется новая пользовательская возможность и она:
- содержит порядок шагов
- затрагивает несколько зависимостей
- меняет правила поведения системы

то это **кандидат в `Application`**, а не в `Presentation` и не в `App`.

---

## 3. Infrastructure

### Назначение
Техническая реализация интеграций и хранения.

`Infrastructure` должна отвечать за вопрос:

> “как именно мы это делаем технически?”

### Что должно жить в Infrastructure

#### 1. Torrent engine integration
- MonoTorrent integration
- engine lifecycle
- engine state restore/save
- runtime registry
- snapshot building from engine state
- background monitoring

#### 2. Persistence
- каталог торрентов
- JSON settings storage
- local app storage
- cache files
- migration between config versions

#### 3. Technical adapters
- filesystem
- URL launchers if they are not platform-shell concerns
- storage repositories
- background task mechanics

### Что важно сохранить из текущего проекта
В текущем проекте уже есть сильные решения, которые стоит не выбрасывать, а удержать и довести:

#### Сильная сторона 1. Разделение infrastructure на маленькие роли
После распила `MonoTorrentService` в проекте уже появились хорошие направления:
- startup coordinator
- command executor
- add executor
- update publisher
- runtime registry
- snapshot factory
- query service

Это нужно сохранить как принцип:
- один класс = одна понятная роль
- orchestrator не знает все детали сразу

#### Сильная сторона 2. Каталог торрентов как отдельная зона
`TorrentCatalogStore` / cache model — важная идея.
Её не нужно убирать.
Нужно только сделать границы более честными:
- каталог — источник сохранённого состояния
- runtime registry — источник живых managers
- application policy решает, как их синхронизировать для пользователя

#### Сильная сторона 3. Отдельные preference services
`JsonAppPreferences` и `JsonTorrentPreferences` — правильное направление.
Это хороший базовый storage adapter.
Но логика применения настроек не должна застревать внутри storage.

### Что нельзя делать в Infrastructure
- показывать пользователю UI без application/presentation policy
- принимать shell-решения
- управлять окном
- определять navigation
- решать, как должен выглядеть конкретный экран

### Важное уточнение про “бизнес-логику в Infrastructure”
Сейчас часть бизнес-правил фактически живёт тут:
- cached/runtime merge
- restore intent
- duplicate detection behavior
- delayed command application

Это допустимый **текущий компромисс**, но не желаемое финальное состояние.
После стабилизации эти правила нужно постепенно поднимать в `Application` и частично в `Domain`.

---

## 4. Presentation

### Назначение
Presentation — это слой экранного состояния и взаимодействия пользователя с UI.

### Что должно жить в Presentation
- ViewModel
- UI command state
- selected item state
- projection списка
- форматирование данных для UI
- presentation-specific adapters для коллекций и projections

### Что уже правильно делается и это стоит сохранить

#### 1. Разделение item VM и screen VM
`TorrentItemViewModel` и `TorrentViewModel` — это правильная линия разделения.
При появлении страницы деталей её нужно продолжить:
- `TorrentListViewModel`
- `TorrentDetailsViewModel`
- `TorrentSettingsViewModel` (per-torrent, если появится)

#### 2. Вынесение list sync в projection
`TorrentListProjection` — хорошее направление.
Это как раз presentation logic, но не сценарная логика.

#### 3. Staged settings editing
Для страниц настроек важно сохранить подход:
- локальное редактирование
- dirty-state
- apply/reload

Это масштабируется лучше, чем auto-save на каждый toggle.

### Что не должно жить в Presentation
- `Process.Start`
- filesystem scanning
- torrent engine details
- recovery logic
- close/tray policy
- сложная orchestration нескольких зависимостей

### Целевая модель Presentation для развития проекта

```text
Presentation/
├─ Shell/
│  ├─ MainViewModel
│  ├─ Navigation state
│  └─ Close prompt state
├─ Torrents/
│  ├─ TorrentListViewModel
│  ├─ TorrentItemViewModel
│  ├─ TorrentDetailsViewModel
│  ├─ TorrentDetailsTabs/*
│  └─ TorrentListProjection
├─ Settings/
│  ├─ GlobalSettingsViewModel
│  ├─ TorrentEngineSettingsViewModel
│  └─ TorrentSpecificSettingsViewModel
└─ Shared/
   ├─ Projection/
   ├─ Formatting/
   └─ Ui state helpers/
```

### Особое правило для будущего экрана подробностей
Двойной клик по торренту не должен открывать “магическое окно с логикой внутри”.
Нужно сразу проектировать это так:
- действие двойного клика → application navigation/use case
- details screen получает `TorrentId`
- читает данные через query service
- настройки торрента — отдельная модель, а не расширение list item VM

---

## 5. App

### Назначение
Это внешний WinUI shell и composition root.

### Что должно жить в App
- WinUI/XAML pages
- window creation
- tray integration
- activation receiving
- navigation shell wiring
- DI assembly
- platform adapters
- dispatcher

### Что уже правильно в текущем проекте
- есть отдельные адаптеры для dialog/picker/dispatcher/tray
- есть осознание, что `App.xaml.cs` нужно разгружать
- platform concerns в целом уже изолируются лучше, чем в начале

### Что должно остаться правилом
`App` можно использовать для:
- wiring
- bootstrap
- platform event bridging

Но нельзя использовать как место, где живут:
- пользовательские сценарии
- бизнес-решения
- правила синхронизации состояния торрентов
- close behavior policy

### Будущее окно выбора при закрытии
Оно должно жить так:
- `App` показывает UI-диалог
- `Application` решает, какие есть допустимые варианты
- close coordinator применяет решение

А не так:
- `App.xaml.cs` сам решает всё целиком

---

# Целевые поддомены / функциональные зоны

Чтобы архитектура не разваливалась при росте, нужно думать не только слоями, но и **функциональными зонами**.

## 1. Torrent List
Отвечает за:
- список торрентов
- агрегированные статусы
- выбор торрента
- команды start/pause/remove

## 2. Torrent Details
Будет отвечать за:
- общую информацию
- файлы
- трекеры
- статистику
- per-torrent settings

## 3. Torrent Intake
Отвечает за:
- open `.torrent`
- add magnet
- preview dialog
- duplicate detection
- destination selection

## 4. Torrent Runtime
Отвечает за:
- lifecycle engine
- runtime registry
- pause/start/remove
- queued intents
- state sync after startup

## 5. Settings
Разделяется на:
- global app settings
- torrent engine settings
- future per-torrent settings

## 6. Shell
Отвечает за:
- main window
- tray
- activation
- close behavior
- navigation

Каждая новая feature должна сначала соотноситься с одной из этих зон.
Если feature размазана сразу по нескольким слоям и зонам, нужен отдельный workflow/coordinator, а не ad-hoc код в `App` или VM.

---

# Целевая модель настроек

Настройки — это отдельная архитектурная зона, потому что она гарантированно будет расти.

## Что должно быть единым правилом
Любая настройка проходит один и тот же жизненный цикл:

1. `Load persisted values`
2. `Bind editable state`
3. `Track dirty state`
4. `Validate`
5. `Apply`
6. `Persist`
7. `Publish side effects if needed`

## Что это означает practically

### В Domain
- типы и ограничения значений

### В Application
- workflow применения настроек
- валидация на сценарном уровне
- решение, что применять сразу, а что только после restart

### В Infrastructure
- JSON storage / migration / versioning

### В Presentation
- staged edit model
- dirty-state
- validation messages
- apply/reload UI

## Важная будущая развилка
Нужно заранее разделить:
- **глобальные настройки приложения**
- **настройки torrent engine**
- **настройки конкретного торрента**

Иначе при росте всё снова упрётся в один giant settings VM.

---

# Целевая модель состояния торрентов

Это ключевая зона проекта.

## Источники состояния
У системы фактически три вида состояния:

### 1. Persisted catalog state
- что было известно при последнем закрытии
- `ShouldRun`
- сохранённые метаданные
- cached snapshot

### 2. Runtime engine state
- live MonoTorrent state
- скорости
- текущий phase
- реальный progress

### 3. User intent before runtime readiness
- команды, пришедшие до полной загрузки движка
- queued start/pause/remove intent

## Целевое правило
Эти три источника нельзя смешивать неявно.
Нужна явная policy:

- persisted catalog — источник начального UX
- runtime state — источник live truth после готовности engine
- queued intent — источник приоритета пользовательского решения до готовности runtime

Эта policy должна быть **описана и локализована**, а не размазана по пяти классам.

---

# Целевая модель событий и обновлений

Это одна из самых чувствительных зон для анти-паттернов.

## Что нужно зафиксировать

### Источник истины для UI-списка
Для списка торрентов источник данных должен быть один логический поток:
- application-facing snapshots

Не напрямую:
- monitor отдельно
- viewmodel отдельно
- tray отдельно
- catalog отдельно

### Рекомендуемая модель

```text
Infrastructure runtime/cache
   -> application-facing snapshot policy
      -> projections / consumers
         -> UI list / tray / details
```

### Что это значит
- `TorrentMonitor` может остаться техническим polling-механизмом
- но `Presentation` не должна зависеть от того, poll это или event
- tray не должен заново вычислять смысл состояния сам
- details screen не должен обходить list flow хаотично

---

# Что сохранить из текущего проекта

Важно не только ругать текущее решение, но и зафиксировать, что уже хорошо.

## Сохраняем без сожаления

### 1. Слоистую структуру решения
Это сильная сторона проекта и хорошая база.

### 2. Разделение App / Presentation / Application / Domain / Infrastructure
Даже если слои ещё неидеальны, это правильный каркас.

### 3. Подход через snapshots
Для torrent UI это хороший boundary format.

### 4. Отдельные preference services
Нужно развивать, а не выбрасывать.

### 5. Отдельные coordinators/executors после декомпозиции
Это уже хороший шаг против god class.

### 6. Staged settings editing
Это хороший UX и хорошая архитектурная модель для растущих настроек.

---

# Что переосмыслить из текущих решений

## 1. Не превращать `MonoTorrentService` снова в центр мира
Даже если он стал тоньше, нельзя снова возвращать туда:
- UI notifications
- policy decisions
- shell concerns
- application workflows

## 2. Не тащить новые сценарии в `TorrentViewModel`
Экран деталей, per-torrent settings, duplicate handling, close prompt logic — всё это не должно превращать `TorrentViewModel` в новую god VM.

## 3. Не делать settings-подсистему как набор несвязанных toggles
Нужна общая модель и reusable abstraction.

## 4. Не размазывать close policy снова между `App.xaml.cs`, tray и settings
Close behavior должен остаться отдельной policy-подсистемой.

## 5. Не смешивать “сохранённое состояние” и “live state” без явной policy
Это уже один раз стало источником тяжёлых багов. Повторять нельзя.

---

# Правила размещения нового кода

## Если добавляется новая фича, сначала задаём 4 вопроса

### Вопрос 1. Это правило предметной области?
Если да — кандидат в `Domain`.

### Вопрос 2. Это сценарий пользователя?
Если да — кандидат в `Application`.

### Вопрос 3. Это отображение/состояние экрана?
Если да — кандидат в `Presentation`.

### Вопрос 4. Это реализация интеграции или storage?
Если да — кандидат в `Infrastructure`.

Если ответ “это про окно/tray/WinUI activation” — это `App`.

---

# Архитектурная цель после следующих этапов

После стабилизации и тестов проект должен прийти к состоянию, где:

- `Domain` содержит реальные инварианты торрент-системы, а не только DTO-like типы
- `Application` становится центром сценариев и policy
- `Infrastructure` реализует engine и persistence без захвата бизнес-решений UI
- `Presentation` описывает экраны, а не сценарные механизмы
- `App` остаётся shell/composition root

И особенно важно:

- новые настройки масштабируются без giant settings VM
- экран деталей торрента добавляется без слома списка
- окно выбора при закрытии возвращается как расширение close-policy, а не как откат к старому `App.xaml.cs`

---

# Практический вывод

Целевая архитектура NiTorrent — это не “чистая архитектура ради чистой архитектуры”.
Это архитектура, которая:
- сохраняет сильные стороны уже существующего проекта
- исправляет конкретные исторические ошибки
- выдерживает рост настроек и UI-сценариев
- не допускает повторного появления анти-паттернов

Главная идея на будущее:

> Всё, что описывает поведение системы для пользователя, должно постепенно подниматься в `Application` и `Domain`, а всё, что описывает техническую реализацию, должно оставаться в `Infrastructure` и `App`.

