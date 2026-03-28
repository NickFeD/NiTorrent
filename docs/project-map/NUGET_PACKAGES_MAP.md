# NuGet packages map

Этот документ нужен для двух целей:

1. Понять, **какие внешние пакеты уже стоят в проекте**.
2. Понять, **что уже реализовано этими пакетами**, чтобы не писать свои дублирующие реализации вручную.

Главное правило: если пакет уже даёт готовую, зрелую и подходящую для проекта функцию, **не нужно изобретать свою версию без сильной причины**.

---

# Как читать этот документ

Для каждого пакета указано:

- **Где используется** — в каком проекте solution он подключён.
- **Для чего нужен** — какая практическая задача решается этим пакетом.
- **Что уже даёт из коробки** — что не нужно писать самостоятельно.
- **Граница ответственности** — в каком слое он допустим.
- **Чего не делать** — типичные ошибки и дублирование того, что пакет уже умеет.

---

# 1. CommunityToolkit.Mvvm

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`
- `src/NiTorrent.Presentation/NiTorrent.Presentation.csproj`

**Для чего нужен:**
Пакет для MVVM-подхода в .NET/WinUI.

**Что уже даёт из коробки:**
- `ObservableObject`
- `ObservableRecipient`
- `RelayCommand`
- `AsyncRelayCommand`
- source generators для `[ObservableProperty]`
- source generators для `[RelayCommand]`
- уведомление об изменении свойств (`INotifyPropertyChanged`)
- удобный шаблон для ViewModel без ручного шаблонного кода

**Что не нужно писать самостоятельно:**
- свою реализацию базовой `ViewModelBase` только ради `INotifyPropertyChanged`
- свои ручные команды `ICommand`, если достаточно `RelayCommand/AsyncRelayCommand`
- свои шаблоны генерации observable properties
- свой “мини-MVVM framework”

**Граница ответственности:**
- допустим в `Presentation`
- допустим частично в `App`, если там есть UI-facing VM или shell state
- не нужен в `Domain`
- не нужен в `Application`
- не нужен в `Infrastructure`

**Чего не делать:**
- не дублировать toolkit собственными велосипедами
- не строить поверх него ещё один “внутренний MVVM framework” без реальной причины
- не переносить toolkit-типы в доменную модель

**Вывод:**
Если задача — состояние ViewModel, команды, свойства, уведомления об изменении, то сначала нужно использовать **CommunityToolkit.Mvvm**, а не писать свою реализацию.

---

# 2. CommunityToolkit.Common

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Вспомогательные типы и утилиты из экосистемы CommunityToolkit.

**Что уже даёт из коробки:**
- общие utility-компоненты toolkit-экосистемы
- некоторые helper-расширения и базовые вспомогательные типы

**Что не нужно писать самостоятельно:**
- мелкие вспомогательные utility-типы, если уже есть подходящий аналог из toolkit

**Граница ответственности:**
- только UI/support слой
- не для доменной модели

**Чего не делать:**
- не тянуть его в ядро ради удобных extension methods
- не завязывать доменную логику на toolkit utilities

**Комментарий:**
Это не ключевой пакет архитектуры, но он может закрывать часть мелкой вспомогательной функциональности.

---

# 3. DevWinUI

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
UI framework и shell helper-слой поверх WinUI.

**Что уже даёт из коробки:**
- готовые паттерны shell/navigation
- AppData / page metadata integration
- часть инфраструктуры для меню, страниц и навигации
- готовые helpers для современного WinUI-приложения

**Что не нужно писать самостоятельно:**
- свой мини-framework для shell/navigation, если текущий пакет покрывает задачу
- свою отдельную систему описания меню/страниц, если DevWinUI уже используется для этого

**Граница ответственности:**
- только `App` / shell / navigation
- не должен проникать в `Application`, `Domain`, `Infrastructure`

**Чего не делать:**
- не смешивать DevWinUI shell-решения с бизнес-логикой
- не строить вокруг него собственный второй слой навигационного framework-а
- не тащить DevWinUI-типы в `Presentation` без необходимости

**Вывод:**
Если задача связана с shell/navigation, сначала нужно проверить, **умеет ли это уже DevWinUI**, а не писать свою систему с нуля.

---

# 4. DevWinUI.Controls

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Набор UI-контролов для DevWinUI/WinUI.

**Что уже даёт из коробки:**
- готовые контролы и UI building blocks
- стилизованные элементы интерфейса

**Что не нужно писать самостоятельно:**
- свои кастомные контролы, если нужный контрол уже есть в DevWinUI.Controls

**Граница ответственности:**
- только UI слой (`App`)

**Чего не делать:**
- не создавать свои версии контролов без необходимости
- не тащить знания о конкретных контролах в `Application`/`Domain`

---

# 5. DevWinUI.ContextMenu

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Поддержка контекстных меню в экосистеме DevWinUI.

**Что уже даёт из коробки:**
- инфраструктуру и элементы для контекстных меню
- готовую интеграцию в shell/UI слой

**Что не нужно писать самостоятельно:**
- свою отдельную систему контекстных меню, если пакет покрывает сценарий

**Граница ответственности:**
- только UI/shell

**Чего не делать:**
- не завязывать доменную логику на структуру контекстного меню

---

# 6. DevWinUI.SourceGenerator

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Source generator для DevWinUI-инфраструктуры.

**Что уже даёт из коробки:**
- автоматическую генерацию части boilerplate-кода DevWinUI
- упрощение конфигурации shell/navigation

**Что не нужно писать самостоятельно:**
- ручную генерацию или дублирование DevWinUI boilerplate

**Граница ответственности:**
- build-time helper для `App`

**Чего не делать:**
- не дублировать руками код, который generator уже создаёт

---

# 7. Microsoft.WindowsAppSDK

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Базовый стек WinUI 3 / Windows App SDK.

**Что уже даёт из коробки:**
- окно приложения
- XAML infrastructure
- dispatcher / UI thread integration
- app lifecycle
- доступ к современным Windows desktop API

**Что не нужно писать самостоятельно:**
- свою оконную инфраструктуру
- свои низкоуровневые WinUI plumbing-компоненты
- свою реализацию XAML UI stack

**Граница ответственности:**
- только `App`

**Чего не делать:**
- не тащить WinUI-specific типы в `Domain`, `Application`, `Infrastructure`

**Вывод:**
Это фундамент UI-приложения. Всё, что связано с окном, XAML и desktop UI, должно опираться на этот стек, а не на самописные альтернативы.

---

# 8. Microsoft.Windows.SDK.BuildTools

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Build-time пакет для Windows SDK toolchain.

**Что уже даёт из коробки:**
- поддержку сборки Windows/WinUI проекта
- корректную работу toolchain для desktop app

**Что не нужно писать самостоятельно:**
- ничего на уровне runtime
- это техническая зависимость для сборки

**Граница ответственности:**
- только build infrastructure

**Чего не делать:**
- не воспринимать его как прикладной пакет

---

# 9. Microsoft.Windows.CsWinRT

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Interop между .NET и WinRT API.

**Что уже даёт из коробки:**
- доступ к Windows Runtime API из C#
- корректные проекции WinRT-типов

**Что не нужно писать самостоятельно:**
- свои interop-обвязки для стандартных WinRT-сценариев

**Граница ответственности:**
- только Windows/UI integration слой

**Чего не делать:**
- не выносить WinRT-зависимость за пределы shell/integration слоя

---

# 10. WinUIEx

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Расширения для WinUI-приложений.

**Что уже даёт из коробки:**
- tray icon
- улучшенные window helpers
- часть shell/lifecycle удобств, которых не хватает в чистом WinUI

**Что не нужно писать самостоятельно:**
- свою реализацию tray icon с нуля, если WinUIEx уже закрывает этот сценарий
- часть низкоуровневых helpers для окна

**Граница ответственности:**
- только `App` / shell / window lifecycle

**Чего не делать:**
- не смешивать WinUIEx helpers с доменными правилами
- не реализовывать вручную tray/window plumbing, если пакет уже даёт нужное поведение

**Вывод:**
Для tray/window integration сначала нужно использовать **WinUIEx**, а не городить отдельную самописную инфраструктуру вокруг Win32/interop.

---

# 11. Microsoft.Xaml.Behaviors.WinUI.Managed

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Behaviors и actions для WinUI/XAML.

**Что уже даёт из коробки:**
- возможность связывать события UI с поведением без лишнего code-behind
- декларативный XAML-подход для части UI wiring

**Что не нужно писать самостоятельно:**
- лишний code-behind только ради простых UI interactions
- свои аналоги behaviors/actions, если стандартных достаточно

**Граница ответственности:**
- только UI/XAML слой

**Чего не делать:**
- не переносить через behaviors бизнес-логику в XAML

---

# 12. Microsoft.Extensions.DependencyInjection

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Контейнер внедрения зависимостей.

**Что уже даёт из коробки:**
- регистрацию сервисов
- singleton/scoped/transient lifetimes
- получение зависимостей через constructor injection

**Что не нужно писать самостоятельно:**
- свой DI-контейнер
- свой service locator framework
- свою систему фабрик для каждой зависимости вручную

**Граница ответственности:**
- composition root (`App`)
- registration boundaries

**Чего не делать:**
- не строить поверх него свой “внутренний контейнер”
- не использовать service locator там, где можно применить normal constructor injection

**Вывод:**
Для DI проект уже использует стандартный стек. **Писать свой DI framework не нужно.**

---

# 13. Microsoft.Extensions.DependencyInjection.Abstractions

**Где используется:**
- `src/NiTorrent.Infrastructure/NiTorrent.Infrastructure.csproj`
- `src/NiTorrent.Presentation/NiTorrent.Presentation.csproj`

**Для чего нужен:**
Контракты DI без обязательной зависимости от полного контейнера.

**Что уже даёт из коробки:**
- `IServiceCollection`
- базовые типы для регистрации сервисов

**Что не нужно писать самостоятельно:**
- свои контракты регистрации зависимостей

**Граница ответственности:**
- registration boundary

**Чего не делать:**
- не переносить DI-детали в доменную логику

---

# 14. Microsoft.Extensions.Hosting

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Host infrastructure для .NET-приложения.

**Что уже даёт из коробки:**
- `HostBuilder`
- единый runtime host
- жизненный цикл сервисов
- фоновые сервисы и общую композицию приложения

**Что не нужно писать самостоятельно:**
- свой хост приложения
- свою инфраструктуру запуска/остановки сервисов
- свою систему инициализации контейнера с нуля

**Граница ответственности:**
- только `App` / composition root / startup-shutdown host layer

**Чего не делать:**
- не превращать host в бизнес-слой
- не тащить hosting concerns в `Domain`

---

# 15. Microsoft.Extensions.Hosting.WindowsServices

**Где используется:**
- `src/NiTorrent.Infrastructure/NiTorrent.Infrastructure.csproj`

**Для чего нужен:**
Расширения hosting-инфраструктуры для Windows services.

**Что уже даёт из коробки:**
- интеграцию host-подхода со сценариями Windows service

**Что не нужно писать самостоятельно:**
- свою адаптацию host-модели к Windows service сценариям, если они действительно нужны

**Граница ответственности:**
- infrastructure/runtime hosting support

**Комментарий:**
Для обычного desktop-приложения это выглядит **кандидатом на будущую ревизию**. Нужно отдельно проверить, действительно ли пакет нужен проекту.

---

# 16. Microsoft.Extensions.Logging

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Стандартный стек логирования .NET.

**Что уже даёт из коробки:**
- `ILogger<T>`
- категории логов
- уровни логирования
- стандартную интеграцию с host и DI

**Что не нужно писать самостоятельно:**
- свой logging framework
- свои интерфейсы логирования общего назначения

**Граница ответственности:**
- app host
- integration logging
- diagnostic plumbing

**Чего не делать:**
- не строить своё логирование поверх своего интерфейса без необходимости
- не использовать logging API как бизнес-событийную модель

---

# 17. Microsoft.Extensions.Logging.Abstractions

**Где используется:**
- `src/NiTorrent.Infrastructure/NiTorrent.Infrastructure.csproj`

**Для чего нужен:**
Абстракции логирования без жёсткой привязки к конкретной реализации.

**Что уже даёт из коробки:**
- `ILogger<T>` контракты
- возможность логировать из infrastructure без знания о конкретном sink/provider

**Что не нужно писать самостоятельно:**
- свои абстракции общего логирования

**Граница ответственности:**
- infrastructure logging boundary

---

# 18. Microsoft.Extensions.Configuration

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`

**Для чего нужен:**
Система конфигурации .NET.

**Что уже даёт из коробки:**
- загрузку конфигурации
- binding настроек
- стандартный конфигурационный pipeline

**Что не нужно писать самостоятельно:**
- свой конфигурационный bootstrap layer, если задача решается через стандартный configuration stack

**Граница ответственности:**
- app configuration layer

**Комментарий:**
У проекта уже есть отдельная линия хранения пользовательских настроек через `Nucs.JsonSettings`, поэтому нужно помнить, что **`Configuration` и `JsonSettings` — не одно и то же**.

---

# 19. Newtonsoft.Json

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`
- `src/NiTorrent.Infrastructure/NiTorrent.Infrastructure.csproj`

**Для чего нужен:**
JSON serialization/deserialization.

**Что уже даёт из коробки:**
- сериализацию моделей в JSON
- десериализацию JSON в модели
- настройки сериализации
- обработку сложных JSON-структур

**Что не нужно писать самостоятельно:**
- свой JSON serializer
- свои low-level JSON mapping helpers для типовых сценариев

**Граница ответственности:**
- persistence
- settings/config serialization
- integration with JSON files

**Чего не делать:**
- не тащить JSON-specific детали в домен
- не строить свой мини serializer над уже подключённым Newtonsoft.Json

**Вывод:**
Если задача — читать/писать JSON-файлы, сначала используется **Newtonsoft.Json**, а не своя схема сериализации.

---

# 20. nucs.JsonSettings / Nucs.JsonSettings

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`
- `src/NiTorrent.Infrastructure/NiTorrent.Infrastructure.csproj`

**Для чего нужен:**
Хранение настроек приложения в JSON через model-based settings.

**Что уже даёт из коробки:**
- typed settings classes
- сохранение/загрузка JSON-настроек
- удобную модель работы с app settings
- инфраструктуру для конфигурационных классов

**Что не нужно писать самостоятельно:**
- свою систему хранения настроек в JSON с нуля
- свой “settings manager”, если задача покрывается `JsonSettings`
- свою ручную инфраструктуру сериализации/десериализации settings-классов

**Граница ответственности:**
- infrastructure settings storage
- app configuration persistence

**Чего не делать:**
- не смешивать storage-модель настроек с product-моделью настроек
- не писать второй параллельный settings framework без необходимости

**Вывод:**
Пакет для настроек **уже установлен**. Если задача — хранить пользовательские настройки, сначала нужно смотреть, может ли это покрыть текущая система на базе `Nucs.JsonSettings`, а не устанавливать новый пакет и не писать свой storage layer.

---

# 21. nucs.JsonSettings.AutoSaveGenerator / Nucs.JsonSettings.AutosaveGenerator

**Где используется:**
- `src/NiTorrent.App/NiTorrent.App.csproj`
- `src/NiTorrent.Infrastructure/NiTorrent.Infrastructure.csproj`

**Для чего нужен:**
Source generator / support package для autosave-поведения `JsonSettings`.

**Что уже даёт из коробки:**
- часть boilerplate для settings-модели
- поддержку автоматического сохранения и связанного шаблонного кода

**Что не нужно писать самостоятельно:**
- ручной autosave boilerplate для каждого settings-класса, если это уже покрывается пакетом

**Граница ответственности:**
- build-time/settings support

**Чего не делать:**
- не писать параллельную ручную систему autosave, если текущая уже закрывает задачу

---

# 22. MonoTorrent

**Где используется:**
- `src/NiTorrent.Infrastructure/NiTorrent.Infrastructure.csproj`

**Для чего нужен:**
Torrent engine.

**Что уже даёт из коробки:**
- `ClientEngine`
- `TorrentManager`
- magnet support
- `.torrent` parsing
- download/upload lifecycle
- pause/start/stop/remove
- engine state persistence hooks
- tracker / peer / metadata handling

**Что не нужно писать самостоятельно:**
- свой torrent engine
- свою реализацию magnet/.torrent парсинга
- свою peer/tracker/download модель
- свои низкоуровневые torrent runtime менеджеры

**Граница ответственности:**
- только `Infrastructure`

**Чего не делать:**
- не пускать `MonoTorrent` в `Domain`
- не пускать `MonoTorrent` в `Application`
- не строить пользовательскую модель коллекции торрентов напрямую на типах MonoTorrent

**Вывод:**
`MonoTorrent` уже закрывает **engine-level** часть. Нужно строить адаптеры и продуктовую модель вокруг него, а не пытаться дублировать engine-функции самостоятельно.

---

# Сводка: что проекту уже не нужно писать руками

## MVVM не нужно писать руками
Потому что уже есть:
- `CommunityToolkit.Mvvm`

Не нужно писать:
- свой `ObservableObject`
- свой `RelayCommand`
- свой мини-MVVM toolkit

## DI не нужно писать руками
Потому что уже есть:
- `Microsoft.Extensions.DependencyInjection`

Не нужно писать:
- свой контейнер
- свой service locator framework

## Логирование не нужно писать руками
Потому что уже есть:
- `Microsoft.Extensions.Logging`
- `Microsoft.Extensions.Logging.Abstractions`

Не нужно писать:
- свой logging framework

## Хранение настроек в JSON не нужно писать руками
Потому что уже есть:
- `Nucs.JsonSettings`
- `Nucs.JsonSettings.AutosaveGenerator`
- `Newtonsoft.Json`

Не нужно писать:
- свой settings storage layer с нуля
- свою ручную JSON settings систему

## Shell / tray / window helpers не нужно писать руками с нуля
Потому что уже есть:
- `Microsoft.WindowsAppSDK`
- `WinUIEx`
- `DevWinUI`

Не нужно писать:
- свою shell/navigation framework
- свою tray infrastructure с нуля, если текущее покрывает задачу

## Torrent engine не нужно писать руками
Потому что уже есть:
- `MonoTorrent`

Не нужно писать:
- свой torrent engine
- свой magnet parser
- свои torrent runtime manager-ы

---

# Главное архитектурное правило

Пакеты должны использоваться **по своей зоне ответственности**:

- `Domain` — без внешних UI/engine/settings пакетов
- `Application` — без WinUI/MonoTorrent/JsonSettings
- `Infrastructure` — engine, storage, JSON, settings backend
- `Presentation` — MVVM/state/projections
- `App` — shell, WinUI, tray, navigation, DI composition root

Проблема проекта не в том, что пакетов мало или много. Проблема возникает, когда:
- логика оказывается не в том слое
- пакет используется вне своей зоны ответственности
- поверх хорошего пакета строится лишний велосипед

---

# Когда можно писать своё решение вместо пакета

Это допустимо только если есть понятная причина, например:
- пакет не покрывает нужный сценарий
- пакет даёт слишком тяжёлое или неподходящее API
- нужно строгое product-specific поведение, которого нет в пакете
- нужно изолировать внешний пакет через адаптер/порт

Но даже в этом случае сначала нужно проверить:
- решает ли это уже установленный пакет
- можно ли взять из него только нужную часть
- можно ли обернуть пакет адаптером вместо переписывания его функции с нуля

