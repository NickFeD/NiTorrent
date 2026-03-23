# NiTorrent — PROJECT_MAP

## Как пользоваться этой картой
Это краткая карта **текущего кода**. Она не заменяет `CURRENT_ARCHITECTURE_STATE.md`, а помогает быстро найти нужный модуль и входные точки по сценарию.

---

## 1. Верхний уровень решения

```text
src/
├─ NiTorrent.App/
├─ NiTorrent.Application/
├─ NiTorrent.Domain/
├─ NiTorrent.Infrastructure/
└─ NiTorrent.Presentation/
```

---

## 2. Где сейчас главный рабочий путь

### Главный runtime boundary
- `src/NiTorrent.Application/Abstractions/ITorrentService.cs`
- `src/NiTorrent.Infrastructure/Torrents/MonoTorrentService.cs`

### Главный workflow boundary
- `src/NiTorrent.Application/Abstractions/ITorrentWorkflowService.cs`
- `src/NiTorrent.Application/Torrents/TorrentWorkflowService.cs`

### Главный UI вход в торрент-сценарии
- `src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs`

---

## 3. Карта по проектам

## 3.1. NiTorrent.App
### Назначение
WinUI host, окно, activation, tray, composition root.

### Ключевые файлы
- `App.xaml.cs`
- `MainWindow.xaml(.cs)`
- `Services/AppLifecycle/AppStartupService.cs`
- `Services/AppLifecycle/AppActivationService.cs`
- `Services/AppLifecycle/AppCloseCoordinator.cs`
- `Services/AppLifecycle/AppShutdownCoordinator.cs`
- `Services/TrayService.cs`
- `Services/TorrentPreviewDialogService.cs`

### Что важно
В этом слое уже не стоит держать новую бизнес-логику. Он должен оставаться shell/platform слоем.

## 3.2. NiTorrent.Presentation
### Назначение
ViewModel, observable UI state, presentation mapping.

### Ключевые файлы
- `Features/Torrents/TorrentViewModel.cs`
- `Features/Torrents/TorrentItemViewModel.cs`
- `Features/Torrents/TorrentPreviewViewModel.cs`
- `Features/Settings/TorrentSettingsViewModel.cs`
- `Features/Shell/MainViewModel.cs`
- `SizeFormatter.cs`

### Что важно
`TorrentViewModel` остаётся главным hotspot'ом UI. Здесь подписка на updates, синхронизация коллекции и часть UX-команд.

## 3.3. NiTorrent.Application
### Назначение
Контракты и use case/workflow слой поверх runtime boundary.

### Ключевые файлы
- `Abstractions/ITorrentService.cs`
- `Abstractions/ITorrentWorkflowService.cs`
- `Abstractions/ITorrentPreferences.cs`
- `Torrents/TorrentWorkflowService.cs`
- `Torrents/TorrentPreviewFlow.cs`
- `Torrents/AddTorrentUseCase.cs`
- `Torrents/StartTorrentUseCase.cs`
- `Torrents/PauseTorrentUseCase.cs`
- `Torrents/RemoveTorrentUseCase.cs`
- `Torrents/ApplyTorrentSettingsUseCase.cs`

## 3.4. NiTorrent.Domain
### Назначение
Текущие snapshot/domain типы плюс seed product-centered модели.

### Ключевые файлы
- `Torrents/TorrentSnapshot.cs`
- `Torrents/TorrentStatus.cs`
- `Torrents/TorrentPhase.cs`
- `Torrents/TorrentEntry.cs`
- `Torrents/TorrentIntent.cs`
- `Torrents/TorrentRuntimeState.cs`
- `Torrents/Policies/*`
- `Settings/AppCloseBehavior.cs`
- `Settings/GlobalTorrentSettings.cs`

### Что важно
`TorrentEntry` и политики пока ещё почти не встроены в рабочий код. Это важно помнить при чтении.

## 3.5. NiTorrent.Infrastructure
### Назначение
MonoTorrent integration, catalog/runtime persistence, update pipeline, settings storage.

### Ключевые файлы
- `Torrents/MonoTorrentService.cs`
- `Torrents/TorrentMonitor.cs`
- `Torrents/TorrentEventOrchestrator.cs`
- `Torrents/TorrentUpdatePublisher.cs`
- `Torrents/TorrentCatalogSnapshotSynchronizer.cs`
- `Torrents/TorrentCatalogStore.cs`
- `Torrents/TorrentStartupCoordinator.cs`
- `Torrents/TorrentStartupRecovery.cs`
- `Torrents/TorrentCommandExecutor.cs`
- `Torrents/TorrentAddExecutor.cs`
- `Torrents/TorrentSettingsApplier.cs`
- `Settings/JsonAppPreferences.cs`
- `Settings/JsonTorrentPreferences.cs`
- `DI/DependencyInjection.cs`

---

## 4. Быстрые маршруты по сценарию

### Startup
`App.xaml.cs` → `AppStartupService` → `ITorrentService.InitializeAsync()` → `MonoTorrentService` → `TorrentStartupCoordinator`

### Add torrent from UI
`TorrentViewModel` → `ITorrentWorkflowService` → `TorrentPreviewFlow` → `ITorrentService.GetPreviewAsync/AddAsync()` → `TorrentAddExecutor`

### Periodic list refresh
`TorrentMonitor` → `ITorrentService.PublishTorrentUpdates()` → `TorrentEventOrchestrator` → `TorrentUpdatePublisher` → `TorrentViewModel`

### Settings save
`TorrentSettingsViewModel.Save()` → `ITorrentPreferences` setters → `ApplyTorrentSettingsUseCase` → `ITorrentService.ApplySettingsAsync()` → `TorrentSettingsApplier`

---

## 5. Что не путать
- `PROJECT_MAP.md` — карта текущих файлов.
- `CURRENT_ARCHITECTURE_STATE.md` — описание текущей архитектуры.
- `TARGET_ARCHITECTURE_V2.md` — документ о цели, а не о текущем коде.
