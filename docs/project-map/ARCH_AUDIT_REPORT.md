# Архитектурный Аудит NiTorrent

Дата аудита: 2026-04-05  
Основания: `TARGET_ARCHITECTURE.md`, `USER_APP_LOGIC.md`, `APPLICATION_CONTRACTS.md`, `ANTI_PATTERNS.md`, `FAILURE_MATRIX.md`, `NFR_SLO.md`.

## Executive Summary

- `Critical`: 3
- `Major`: 4
- `Minor`: 1

Ключевой вывод: архитектурные границы в целом соблюдены (UI не ходит в Infrastructure напрямую, пользовательские команды идут через application contracts, второго command-path не обнаружено), но есть критические расхождения по deferred replay, приоритету user intent над runtime refresh и user-facing обработке `open folder`.

## Нарушения По Слоям

### Domain

1. Runtime facts могут перезаписывать runtime-представление вопреки persisted intent.
- Статус: `Critical`
- Подтверждение: [TorrentCollectionRestorePolicy.cs:35](C:/GitHub/NiTorrent/src/NiTorrent.Domain/Torrents/TorrentCollectionRestorePolicy.cs:35)

### Application

1. Deferred replay не гарантирован после startup-цикла (вызов workflow только на restore).
- Статус: `Critical`
- Подтверждение: [RestoreTorrentCollectionWorkflow.cs:40](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/Restore/RestoreTorrentCollectionWorkflow.cs:40)

2. Блокирующие вызовы `.GetAwaiter().GetResult()` в shell workflows/query.
- Статус: `Major`
- Подтверждение: [HandleWindowCloseWorkflow.cs:10](C:/GitHub/NiTorrent/src/NiTorrent.Application/Shell/HandleWindowCloseWorkflow.cs:10), [GetShellStateQuery.cs:14](C:/GitHub/NiTorrent/src/NiTorrent.Application/Shell/GetShellStateQuery.cs:14)

### Infrastructure

1. Sync-failure в read feed проглатывается без логирования.
- Статус: `Major`
- Подтверждение: [EngineBackedTorrentReadModelFeed.cs:72](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Torrents/EngineBackedTorrentReadModelFeed.cs:72)

2. Runtime error попадает в read model как raw `ToString()`.
- Статус: `Major`
- Подтверждение: [RuntimeBackedTorrentRuntimeFactsProvider.cs:84](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Torrents/RuntimeBackedTorrentRuntimeFactsProvider.cs:84)

### Presentation

1. Сценарий `open folder` не обернут в диалоговую обработку ошибки на UI-границе.
- Статус: `Critical`
- Подтверждение: [TorrentViewModel.cs:193](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs:193), [FolderLauncher.cs:12](C:/GitHub/NiTorrent/src/NiTorrent.App/Services/FolderLauncher.cs:12)

2. `Paused/Stopped` отображаются как разные пользовательские состояния.
- Статус: `Major`
- Подтверждение: [TorrentItemViewModel.cs:77](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:77), [TorrentItemViewModel.cs:78](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:78), [TorrentStateToBadgeStyleConverter.cs:19](C:/GitHub/NiTorrent/src/NiTorrent.App/Converters/TorrentStateToBadgeStyleConverter.cs:19), [TorrentStateToBadgeStyleConverter.cs:20](C:/GitHub/NiTorrent/src/NiTorrent.App/Converters/TorrentStateToBadgeStyleConverter.cs:20)

## Сценарии Пользователя (USER_APP_LOGIC, Section 3)

Матрица `Scenario -> Implemented/Partial/Missing -> file refs`:

| Scenario | Status | File refs |
|---|---|---|
| add `.torrent` через unified flow | Implemented | [AddTorrentFileWithPreviewUseCase.cs:9](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/AddTorrentFileWithPreviewUseCase.cs:9), [TorrentPreviewFlow.cs:15](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/TorrentPreviewFlow.cs:15) |
| add magnet через unified flow | Implemented | [AddMagnetUseCase.cs:12](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/AddMagnetUseCase.cs:12), [TorrentPreviewFlow.cs:15](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/TorrentPreviewFlow.cs:15) |
| duplicate handling | Implemented | [TorrentPreviewFlow.cs:31](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/TorrentPreviewFlow.cs:31), [AddTorrentUseCase.cs:14](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/AddTorrentUseCase.cs:14) |
| remove mode choice | Implemented | [TorrentPage.xaml:110](C:/GitHub/NiTorrent/src/NiTorrent.App/Views/TorrentPage.xaml:110), [TorrentPage.xaml:113](C:/GitHub/NiTorrent/src/NiTorrent.App/Views/TorrentPage.xaml:113), [TorrentPage.xaml:117](C:/GitHub/NiTorrent/src/NiTorrent.App/Views/TorrentPage.xaml:117) |
| open folder | Partial | [OpenTorrentFolderUseCase.cs:5](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/OpenTorrentFolderUseCase.cs:5), [TorrentViewModel.cs:193](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs:193), [FolderLauncher.cs:12](C:/GitHub/NiTorrent/src/NiTorrent.App/Services/FolderLauncher.cs:12) |
| close/tray/exit | Implemented | [HandleTrayExitWorkflow.cs:8](C:/GitHub/NiTorrent/src/NiTorrent.Application/Shell/HandleTrayExitWorkflow.cs:8), [AppCloseCoordinator.cs:36](C:/GitHub/NiTorrent/src/NiTorrent.App/Services/AppLifecycle/AppCloseCoordinator.cs:36), [AppCloseCoordinator.cs:73](C:/GitHub/NiTorrent/src/NiTorrent.App/Services/AppLifecycle/AppCloseCoordinator.cs:73) |

## Startup / Restore / Deferred Actions

Проверка инвариантов section 4/6 `USER_APP_LOGIC.md`:

1. Ранний показ списка из каталога.
- Вердикт: `Implemented`
- Подтверждение: [EngineBackedTorrentReadModelFeed.cs:32](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Torrents/EngineBackedTorrentReadModelFeed.cs:32), [GetTorrentListQuery.cs:9](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/Queries/GetTorrentListQuery.cs:9)

2. Отложенные `Start/Pause/Remove` до готовности engine.
- Вердикт: `Partial`
- Что есть: intent/deferred сначала сохраняются в repository, потом immediate apply best-effort.
- Подтверждение: [TorrentCommandService.cs:34](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/Commands/TorrentCommandService.cs:34), [TorrentCommandService.cs:55](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/Commands/TorrentCommandService.cs:55)

3. Применение intent после старта engine.
- Вердикт: `Partial`
- Что есть: replay в restore workflow.
- Ограничение: нет гарантии повторного replay вне startup.
- Подтверждение: [RestoreTorrentCollectionWorkflow.cs:40](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/Restore/RestoreTorrentCollectionWorkflow.cs:40), [ApplyDeferredTorrentActionsWorkflow.cs:34](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/Deferred/ApplyDeferredTorrentActionsWorkflow.cs:34)

## State Mapping Audit

1. Единая пользовательская модель состояний.
- Вердикт: `Partial`
- Причина: формально модель есть, но есть расхождения в пользовательской проекции.

2. `Paused/Stopped` как единое UI-состояние.
- Вердикт: `Missing`
- Подтверждение: [TorrentItemViewModel.cs:77](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:77), [TorrentItemViewModel.cs:78](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:78), [TorrentStateToBadgeStyleConverter.cs:19](C:/GitHub/NiTorrent/src/NiTorrent.App/Converters/TorrentStateToBadgeStyleConverter.cs:19), [TorrentStateToBadgeStyleConverter.cs:20](C:/GitHub/NiTorrent/src/NiTorrent.App/Converters/TorrentStateToBadgeStyleConverter.cs:20)

3. Runtime state не ломает user intent.
- Вердикт: `Missing` (для части путей sync/projection)
- Подтверждение: [TorrentCollectionRestorePolicy.cs:35](C:/GitHub/NiTorrent/src/NiTorrent.Domain/Torrents/TorrentCollectionRestorePolicy.cs:35)

## Settings Consistency Report

1. `read/edit/save/apply` pattern.
- Вердикт: `Implemented`
- Подтверждение: [TorrentSettingsViewModel.cs:95](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Settings/TorrentSettingsViewModel.cs:95), [TorrentSettingsViewModel.cs:153](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Settings/TorrentSettingsViewModel.cs:153), [TorrentSettingsService.cs:15](C:/GitHub/NiTorrent/src/NiTorrent.Application/Settings/TorrentSettingsService.cs:15)

2. Единообразие поведения на странице.
- Вердикт: `Implemented`

3. Отсутствие второго source of truth.
- Вердикт: `Partial`
- Причина: существует mutable-контракт `ITorrentPreferences` с авто-save setter-путем.
- Подтверждение: [ITorrentPreferences.cs:8](C:/GitHub/NiTorrent/src/NiTorrent.Application/Abstractions/ITorrentPreferences.cs:8), [JsonTorrentPreferences.cs:13](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Settings/JsonTorrentPreferences.cs:13)

## Failure Handling Coverage

Проверка соответствия `FAILURE_MATRIX.md` + `NFR_SLO.md`:

1. Пользовательские сообщения без raw exceptions.
- Вердикт: `Partial`
- Риск: raw runtime error в UI.
- Подтверждение: [RuntimeBackedTorrentRuntimeFactsProvider.cs:84](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Torrents/RuntimeBackedTorrentRuntimeFactsProvider.cs:84), [TorrentItemViewModel.cs:65](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:65)

2. Safe degradation.
- Вердикт: `Implemented/Partial`
- Best-effort sync есть, но без telemetry на catch-path.
- Подтверждение: [EngineBackedTorrentReadModelFeed.cs:72](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Torrents/EngineBackedTorrentReadModelFeed.cs:72)

3. Best-effort/retry.
- Вердикт: `Partial`
- Риск: deferred replay не гарантируется после startup.

## Contract Violations (APPLICATION_CONTRACTS + ANTI_PATTERNS)

### Critical

1. Нет гарантированного deferred replay вне restore startup.
- Нарушает: command/deferred guarantees.
- Подтверждение: [RestoreTorrentCollectionWorkflow.cs:40](C:/GitHub/NiTorrent/src/NiTorrent.Application/Torrents/Restore/RestoreTorrentCollectionWorkflow.cs:40)

2. Runtime refresh может неявно ослаблять persisted intent.
- Нарушает: intent stronger than runtime transitions.
- Подтверждение: [TorrentCollectionRestorePolicy.cs:35](C:/GitHub/NiTorrent/src/NiTorrent.Domain/Torrents/TorrentCollectionRestorePolicy.cs:35)

3. `open folder` не обеспечивает стабильный user-facing error flow на UI-границе.
- Нарушает: user scenario guarantees + message policy.
- Подтверждение: [TorrentViewModel.cs:193](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs:193)

### Major

1. `Paused/Stopped` не унифицированы.
- Подтверждение: [TorrentItemViewModel.cs:77](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:77), [TorrentItemViewModel.cs:78](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:78)

2. Raw runtime exception details попадают в UI.
- Подтверждение: [RuntimeBackedTorrentRuntimeFactsProvider.cs:84](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Torrents/RuntimeBackedTorrentRuntimeFactsProvider.cs:84), [TorrentItemViewModel.cs:65](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:65)

3. Нет логирования sync-failure в read feed.
- Подтверждение: [EngineBackedTorrentReadModelFeed.cs:72](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Torrents/EngineBackedTorrentReadModelFeed.cs:72)

4. Blocking sync calls в application shell workflows.
- Подтверждение: [HandleWindowCloseWorkflow.cs:10](C:/GitHub/NiTorrent/src/NiTorrent.Application/Shell/HandleWindowCloseWorkflow.cs:10), [GetShellStateQuery.cs:14](C:/GitHub/NiTorrent/src/NiTorrent.Application/Shell/GetShellStateQuery.cs:14)

### Minor

1. Mutable settings preferences контракт допускает обход единого settings use-case.
- Подтверждение: [ITorrentPreferences.cs:8](C:/GitHub/NiTorrent/src/NiTorrent.Application/Abstractions/ITorrentPreferences.cs:8), [JsonTorrentPreferences.cs:13](C:/GitHub/NiTorrent/src/NiTorrent.Infrastructure/Settings/JsonTorrentPreferences.cs:13)

---

## Дополнение: Контрольная Проверка После Внесения Правок (2026-04-05)

Ниже — дополнительные недостатки, выявленные на повторной проверке.

### Major

1. `AskUser` close behavior не реализован: используется fallback на полный выход.
- Риск: неявное поведение, не зафиксированное как продуктово утверждённый сценарий.
- Подтверждение: [AppCloseCoordinator.cs:53](C:/GitHub/NiTorrent/src/NiTorrent.App/Services/AppLifecycle/AppCloseCoordinator.cs:53), [AppCloseCoordinator.cs:54](C:/GitHub/NiTorrent/src/NiTorrent.App/Services/AppLifecycle/AppCloseCoordinator.cs:54)

2. Страница темы обходит единый settings lifecycle (`read/edit/save/apply`) и применяет изменения напрямую через attach-сервис.
- Риск: нарушение единообразия настроек и контрактной модели.
- Подтверждение: [ThemeSettingPage.xaml:21](C:/GitHub/NiTorrent/src/NiTorrent.App/Views/Settings/ThemeSettingPage.xaml:21), [ThemeSettingPage.xaml:33](C:/GitHub/NiTorrent/src/NiTorrent.App/Views/Settings/ThemeSettingPage.xaml:33)

3. В details-экране статус показывается как raw enum (`Phase.ToString()`), а не как user-facing проекция.
- Риск: нестабильная/техническая формулировка состояния в UI.
- Подтверждение: [TorrentDetailsViewModel.cs:71](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentDetailsViewModel.cs:71)

4. В `TorrentItemViewModel` присутствуют строковые ресурсы с повреждённой кодировкой (mojibake) для статусов.
- Риск: нечитаемые пользовательские тексты.
- Подтверждение: [TorrentItemViewModel.cs:63](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:63), [TorrentItemViewModel.cs:67](C:/GitHub/NiTorrent/src/NiTorrent.Presentation/Features/Torrents/TorrentItemViewModel.cs:67)

### Re-validated During This Pass

1. Unified add flow для `.torrent` и magnet подтверждён.
2. Remove mode choice (с/без данных) подтверждён в UI.
3. `Paused/Stopped` отображаются как единое пользовательское состояние в списке/бейджах.
