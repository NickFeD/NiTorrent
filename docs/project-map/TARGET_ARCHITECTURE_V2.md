# NiTorrent — TARGET_ARCHITECTURE_V2

## Зачем нужен этот документ
Этот документ описывает **целевую архитектуру новой версии проекта**, а не аккуратную версию текущего состояния.

Он строится от:
- `USER_APP_LOGIC.md`
- `ANTI_PATTERNS.md`
- уже найденных сильных сторон текущего проекта
- будущих требований продукта

Цель документа — задать архитектуру, которая:
- выражает пользовательскую логику напрямую;
- не тащит бизнес-правила в infrastructure;
- не плодит временные фасады и мосты как постоянную часть системы;
- выдерживает рост настроек, экран деталей торрента и более сложный close-flow.

---

## 1. Исходная продуктовая модель
Из `USER_APP_LOGIC.md` следует, что ядро приложения — это **не MonoTorrent engine**, а:
- пользовательская коллекция торрентов;
- пользовательские намерения для каждого торрента;
- глобальные настройки приложения;
- будущие настройки конкретного торрента;
- shell-политики уровня продукта.

Это означает:
- торрент как продуктовая сущность существует даже до полной инициализации движка;
- действия пользователя должны приниматься системой до готовности движка;
- кеш списка — это не временная уловка UI, а часть продуктовой модели;
- runtime-движок даёт факты о состоянии, но не определяет сам, что «существует для пользователя».

### Главный принцип
**Источник истины на уровне продукта — пользовательская коллекция и пользовательское намерение.**

MonoTorrent должен быть:
- адаптером внешнего движка;
- поставщиком runtime-фактов;
- исполнителем команд.

MonoTorrent не должен быть центром бизнес-логики.

---

## 2. Что считаем бизнес-логикой
На основе `USER_APP_LOGIC.md` к бизнес-логике относятся правила:
- торрент не должен исчезать из пользовательской коллекции из-за проблем движка;
- повторное добавление того же торрента не должно плодить дубликаты;
- duplicate должен превращаться в понятный продуктовый результат;
- пользовательское намерение `Run`/`Pause` должно переживать перезапуск;
- действия до готовности движка должны приниматься и применяться позже;
- paused-торрент после запуска остаётся на паузе;
- running-торрент после запуска снова стартует;
- удаление, сделанное до готовности движка, должно быть доведено до конца позже;
- кеш и runtime должны синхронизироваться без потери списка;
- настройки должны работать единообразно;
- close-flow подчиняется пользовательской настройке и явной команде `Выход` из трея.

Эти правила должны жить в `Domain` и `Application`, а не как побочный эффект инфраструктурного кода.

---

## 3. Целевая структура решения

```text
NiTorrent.App
  └─ composition root, shell adapters, WinUI host

NiTorrent.Presentation
  └─ screens, view models, projections, navigation state, UI formatting

NiTorrent.Application
  └─ commands, queries, workflows, orchestration, application policies

NiTorrent.Domain
  └─ entities, value objects, invariants, domain services, domain policies

NiTorrent.Infrastructure
  └─ engine adapter, repositories, settings storage, OS/file/dialog adapters
```

Базовое правило зависимостей:

```text
App -> Presentation -> Application -> Domain
App -> Infrastructure -> Application -> Domain
Infrastructure -/-> Presentation
Infrastructure -/-> App
Domain -/-> Infrastructure
```

---

## 4. Целевая роль каждого слоя

## 4.1. Domain
Domain должен стать местом, где живут **правила предметной области торрент-клиента**, а не только DTO.

### Что должно жить в Domain
#### Сущности и value objects
- `TorrentEntry` или `TorrentAggregate`
- `TorrentId`
- `TorrentKey`
- `TorrentIntent` (`Run`, `Pause`, future `RemovePending`)
- `TorrentLifecycleState`
- `TorrentRuntimeState`
- `TorrentStatus`
- `SaveLocation`
- `TorrentProgress`
- `AppCloseBehavior`
- `GlobalTorrentSettings`
- `PerTorrentSettings` (future)
- `PendingUserAction` / `DeferredAction` для действий до готовности движка

#### Domain rules
- правило определения дубликата;
- правило сочетания `Intent + RuntimeFacts -> EffectiveStatus`;
- правило восстановления пользовательской коллекции после старта;
- правило обработки deferred user actions;
- правило допустимых переходов состояний;
- правило close behavior;
- правила валидности настроек.

#### Domain services
Только там, где логика слишком крупная для одной сущности:
- `TorrentDuplicatePolicy`
- `TorrentRestorePolicy`
- `TorrentStatusResolver`
- `DeferredActionPolicy`
- `CloseBehaviorPolicy`

### Что не должно жить в Domain
- MonoTorrent types
- JSON storage
- файловая система
- WinUI/XAML
- tray API
- логирование инфраструктуры
- прямые ссылки на репозитории и адаптеры

---

## 4.2. Application
Application — слой пользовательских сценариев и orchestration.

### Что должно жить в Application
#### Commands / use cases
- `AddTorrentFileCommand`
- `AddMagnetCommand`
- `PreviewTorrentCommand`
- `StartTorrentCommand`
- `PauseTorrentCommand`
- `RemoveTorrentCommand`
- `ApplyGlobalSettingsCommand`
- `RequestMainWindowCloseCommand`
- `RequestExplicitExitCommand`
- `OpenTorrentDetailsCommand` (future)
- `UpdatePerTorrentSettingsCommand` (future)

#### Queries
- `GetTorrentListQuery`
- `GetTorrentDetailsQuery`
- `GetSettingsQuery`
- `GetShellStateQuery`

#### Workflows
- startup restore workflow;
- file activation workflow;
- preview + confirm + add workflow;
- close workflow;
- settings apply workflow;
- future details workflow.

#### Порты наружу
- `ITorrentEngineGateway`
- `ITorrentCollectionRepository`
- `ISettingsRepository`
- `IShellGateway`
- `IDialogGateway`
- `IFilePickerGateway`
- `IFolderLauncherGateway`
- `IClock`

### Принцип
Application управляет сценарием, но не принимает инфраструктурные детали за продуктовую истину.

### Что не должно жить в Application
- конкретные MonoTorrent-классы;
- raw JSON model;
- UI controls/pages;
- низкоуровневый WinUI lifecycle.

---

## 4.3. Presentation
Presentation отвечает за отображение и локальное UI-состояние.

### Что должно жить в Presentation
- страницы и экраны;
- ViewModel;
- projections/read-side adapters;
- форматирование значений для UI;
- локальное состояние формы настроек;
- выбранный торрент;
- навигация к подробностям.

### Что допустимо
- `TorrentListProjection`
- будущий `TorrentDetailsProjection`
- `SettingsDraftViewModel`
- UI-only сортировка/фильтрация, если она не меняет продуктовую модель.

### Что не должно жить в Presentation
- duplicate policy;
- restore policy;
- queued/deferred action logic;
- shell-product policy;
- прямые вызовы OS/Process/engine.

### Правило для ViewModel
ViewModel должна знать:
- что показать;
- что выбрано;
- какую команду запросить у Application;
- какие поля формы изменены.

ViewModel не должна знать:
- как устроен каталог торрентов;
- как синхронизируются cache/runtime;
- как engine хранит managers;
- как работает deferred execution до готовности движка.

---

## 4.4. Infrastructure
Infrastructure должна стать слоем реализации портов, а не местом product decisions.

### Что должно жить в Infrastructure
#### Engine adapter
- запуск и остановка MonoTorrent;
- выполнение команд движка;
- получение runtime facts;
- чтение восстановленных manager'ов.

#### Persistence adapters
- хранение пользовательской коллекции торрентов;
- хранение глобальных настроек;
- future per-torrent settings storage;
- миграции формата настроек/каталога.

#### OS/shell adapters
- tray;
- WinUI dialogs;
- file picker;
- folder launcher;
- URI/file activation adapters.

### Что не должно жить в Infrastructure
- правило, должен ли торрент быть запущен после рестарта;
- правило, что считать дубликатом;
- правило, как merge'ить кеш и runtime на уровне продукта;
- политика close behavior;
- продуктовые решения по deferred actions.

### Допустимый компромисс
В infrastructure может оставаться технический orchestration, если он касается только bridging к внешней библиотеке, но он не должен менять продуктовые решения.

---

## 5. Новая модель торрента
Текущего `TorrentSnapshot` недостаточно как ядра системы. Он полезен как read model, но не как продуктовая сущность.

## 5.1. Domain aggregate
Минимально целевой `TorrentEntry` должен содержать:
- `TorrentId`
- `TorrentKey`
- `Name`
- `SavePath`
- `AddedAt`
- `Intent`
- `LifecycleState`
- `RuntimeState?`
- `LastKnownStatus`
- `HasMetadata`
- `SelectedFiles` или ссылку на их продуктовую модель
- `PerTorrentSettings?`

## 5.2. Почему это нужно
Потому что с точки зрения пользователя торрент:
- существует до старта движка;
- должен переживать рестарт;
- должен принимать команды до готовности движка;
- может иметь product-owned intent, не совпадающий с мгновенным runtime-state.

## 5.3. Read model
Отдельно от доменной сущности нужны проекции:
- `TorrentListItem`
- `TorrentDetailsModel`
- `TorrentSpeedSummaryModel`

UI должен зависеть от read models, а не от внутренней доменной сущности напрямую.

---

## 6. Новая модель настроек
Из `USER_APP_LOGIC.md` следует, что все настройки должны быть единообразны по UX и по архитектуре.

### 6.1. Категории настроек
#### Глобальные настройки приложения
- close behavior;
- shell/tray behavior;
- UI preferences;
- future update checks.

#### Глобальные торрент-настройки
- default save path;
- speed limits;
- DHT / peer discovery / port forwarding;
- metadata/fast resume behavior.

#### Настройки конкретного торрента
- priority/files selection;
- torrent-specific speed limits;
- path override;
- auto-start policy.

### 6.2. Целевая схема работы
- Domain задаёт значения и правила валидности;
- Application управляет `load -> edit draft -> validate -> apply -> persist`;
- Infrastructure только хранит и загружает;
- Presentation показывает staged draft с единым UX `apply/reset`.

### 6.3. Принцип расширяемости
Добавление новой настройки не должно требовать:
- нового storage-подхода;
- новой UX-механики;
- прямого вызова infrastructure из ViewModel.

---

## 7. Целевая модель startup / restore
Это ключевой сценарий проекта.

### Целевая последовательность
1. Поднимается shell и базовые UI-сервисы.
2. Загружается пользовательская коллекция торрентов из репозитория.
3. UI получает раннюю read model из product-owned collection.
4. Пользователь уже может выполнять команды.
5. Startup workflow поднимает движок как infrastructure adapter.
6. Runtime facts синхронизируются с доменной моделью.
7. Применяются сохранённые intents и deferred actions.
8. UI получает обновлённую согласованную read model.

### Ключевое правило
Кеш списка — это часть продуктовой модели, а не временный fallback для красивого старта.

### Ещё одно правило
Runtime engine state не имеет права сам по себе уничтожать пользовательскую коллекцию.

---

## 8. Целевая модель deferred actions
`USER_APP_LOGIC.md` явно требует, чтобы действия пользователя до готовности движка не терялись.

Поэтому в новой архитектуре нужен явный механизм:
- пользовательская команда принимается сразу;
- в domain фиксируется новое намерение или deferred action;
- application workflow применяет его при готовности нужной инфраструктуры.

Это значит, что:
- deferred commands — часть продуктовой модели;
- они не должны быть случайной реализацией внутри infrastructure startup-кода.

---

## 9. Целевая модель update-flow
Текущая hybrid-модель допустима только как переходная.

### Целевая схема
```text
Engine facts -> Application synchronization -> Domain state update -> Read model projection -> UI
```

### Что это значит на практике
- engine adapter не публикует продуктовую истину напрямую в UI;
- application синхронизирует facts с доменной моделью;
- projections строятся из согласованного application/domain state;
- tray, список и детали не считают одну и ту же бизнес-логику каждый по-своему.

### Что должно исчезнуть
- разные низкоуровневые подписки на разные snapshot-потоки;
- ручной merge cache/runtime в нескольких местах;
- side effects внутри publish-методов.

---

## 10. Целевая shell / close архитектура
Close-flow должен быть policy-driven и расширяемым.

### Сейчас продукт требует
- `MinimizeToTray`
- `Exit`

### В будущем
- `AskUser`

### Целевая форма
#### Domain
- `AppCloseBehavior`
- `CloseBehaviorPolicy`

#### Application
- `HandleWindowCloseWorkflow`
- `HandleTrayExitWorkflow`
- future `AskCloseBehaviorWorkflow`

#### Infrastructure/App
- hide/show window;
- hide/show tray;
- terminate app.

Это позволит вернуть окно выбора при закрытии без возврата логики в `App.xaml.cs`.

---

## 11. Экран деталей торрента как обязательный архитектурный тест
Будущий double-click в списке должен открывать экран деталей.

Архитектура обязана поддержать это без хака.

### Для этого нужны
- `GetTorrentDetailsQuery`
- `OpenTorrentDetailsCommand`
- `TorrentDetailsProjection`
- future `UpdatePerTorrentSettingsCommand`
- отдельный navigation/use-case поток

### Принцип
Список и экран деталей не должны делить один mutable UI-object как источник истины.

---

## 12. Что стоит сохранить из текущего проекта
Из текущего проекта стоит сохранить:
- разбиение на проекты;
- staged settings UX как направление;
- уже найденную важность `ShouldRun`/intent-подобной модели;
- use-case слой как seed для application;
- projections как seed для будущего read-side;
- осознание anti-patterns и уже накопленные docs.

Но это надо сохранить как материал для миграции, а не как ограничение новой архитектуры.

---

## 13. Что нужно переосмыслить полностью
### 13.1. `MonoTorrentService` как центр мира
Новая архитектура не должна иметь один главный фасад, который знает всё.

### 13.2. Snapshot-first мышление
`snapshot` должен остаться read model, а не универсальной заменой домена.

### 13.3. Infrastructure orchestration
Если правило влияет на пользовательскую логику, ему не место в infrastructure.

### 13.4. Случайные переходные мосты
Нельзя закреплять temporary abstractions как постоянный слой.

---

## 14. Чего в новой архитектуре не должно быть
Согласно `ANTI_PATTERNS.md`, новая архитектура не должна допускать:
- god classes;
- orchestration в `App.xaml.cs`;
- бизнес-логику в infrastructure;
- hidden side effects;
- смешение push/pull без одной официальной модели;
- разные механики настроек для разных страниц;
- UI, который напрямую реализует продуктовые правила;
- переходные фасады без плана удаления.

---

## 15. Критерии успеха
Новая архитектура считается достигнутой, когда:
- пользовательская логика из `USER_APP_LOGIC.md` выражается напрямую через Domain + Application;
- список торрентов и пользовательские намерения не зависят напрямую от runtime engine state;
- действия до готовности движка являются частью модели, а не хака;
- расширение настроек идёт по одному шаблону;
- экран деталей торрента добавляется как новый slice без перестройки списка;
- возврат окна выбора при закрытии не ломает close-flow;
- разработчик может точно сказать, где живёт каждое правило системы.
