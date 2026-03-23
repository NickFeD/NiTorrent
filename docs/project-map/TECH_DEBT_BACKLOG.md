# NiTorrent — TECH_DEBT_BACKLOG

## Назначение
Этот список фиксирует **актуальный технический долг по текущему коду**. Он не описывает всю целевую архитектуру разом, а помогает видеть ближайшие честные шаги.

Приоритеты ниже исходят из текущего рабочего состояния: layered application с главным runtime фасадом `ITorrentService/MonoTorrentService`.

---

## Critical

### TD-001. `MonoTorrentService` остаётся главным runtime фасадом
**Статус:** active  
**Файлы:** `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`

**Проблема**
Один фасад до сих пор держит слишком много обязанностей и служит главным boundary для всего приложения.

**Почему важно**
Пока это так, любой следующий крупный сценарий будет естественно добавляться в этот класс.

**Следующий честный шаг**
Не делать новый god class рядом. Сначала выделять узкие engine/runtime boundaries и переносить orchestration выше.

---

### TD-002. Snapshot-based update pipeline остаётся главным read-side
**Статус:** active  
**Файлы:**
- `TorrentMonitor.cs`
- `TorrentEventOrchestrator.cs`
- `TorrentUpdatePublisher.cs`
- `TorrentCatalogSnapshotSynchronizer.cs`
- `TorrentViewModel.cs`

**Проблема**
Список торрентов зависит от смешанной модели polling + publish + catalog sync.

**Риск**
Регрессии вида:
- дубли cached/live элементов;
- неправильное исчезновение элементов;
- рассинхронизация UI после startup/restore.

---

### TD-003. Product-centered domain model ещё не интегрирована в рабочий путь
**Статус:** active  
**Файлы:** `src/NiTorrent.Domain/Torrents/*`, `src/NiTorrent.Domain/Settings/*`

**Проблема**
`TorrentEntry`, `TorrentIntent`, `DeferredAction`, `AppCloseBehavior` и политики уже есть, но почти не участвуют в живом коде.

**Риск**
Архитектура раздваивается: один путь документируется как целевой, другой реально исполняется.

---

## High

### TD-004. Close behavior всё ещё живёт через legacy bool
**Статус:** active  
**Файлы:**
- `ITorrentPreferences.cs`
- `JsonTorrentPreferences.cs`
- `TorrentConfig.cs`
- `AppCloseCoordinator.cs`

**Проблема**
Рабочий shell flow опирается на `MinimizeToTrayOnClose`, хотя в домене уже есть `AppCloseBehavior`.

---

### TD-005. Settings apply не атомарен
**Статус:** active  
**Файлы:**
- `TorrentSettingsViewModel.cs`
- `JsonTorrentPreferences.cs`
- `ApplyTorrentSettingsUseCase.cs`
- `TorrentSettingsApplier.cs`

**Проблема**
Форма staged-edit хорошая, но backend persistence и runtime apply разделены неявно.

---

### TD-006. `TorrentViewModel` остаётся главным UI hotspot
**Статус:** active  
**Файлы:** `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`

**Проблема**
В одном месте всё ещё совмещены:
- подписка на updates;
- sync UI collection;
- aggregate speed;
- UX команды;
- error dialogs.

---

### TD-007. Startup/restore зависит от хрупкого порядка инициализации
**Статус:** active  
**Файлы:**
- `App.xaml.cs`
- `AppStartupService.cs`
- `MonoTorrentService.cs`
- `TorrentStartupCoordinator.cs`
- `TorrentStartupRecovery.cs`

**Проблема**
Порядок публикации cached state, запуска engine и появления live updates важен и легко ломается.

---

## Medium

### TD-008. Документация о цели и документация о текущем состоянии легко путаются
**Статус:** active

**Проблема**
Без `CURRENT_ARCHITECTURE_STATE.md` и явного разделения документов разработчик легко читает target-doc как описание уже собранного кода.

---

### TD-009. `FileLogger` использует синхронный gate в runtime path логирования
**Статус:** active  
**Файлы:** `src/NiTorrent.App/Services/FileLogger.cs`

**Проблема**
Логирование сейчас достаточно простое, но синхронная блокировка может стать bottleneck при большом числе ошибок/логов.

---

### TD-010. Не хватает явной sequence-документации для критичных сценариев
**Статус:** active

**Проблема**
Даже при наличии map-документов новичку трудно быстро собрать в голове точные runtime sequence для startup, activation и update-flow.

**Полезный следующий шаг**
Добавить отдельные sequence sections или простые диаграммы для:
- startup;
- file activation;
- add preview flow;
- list update flow;
- close/shutdown.
