# План Исправлений По Архитектурному Аудиту

Дата: 2026-04-05  
Связанный отчёт: [ARCH_AUDIT_REPORT.md](C:/GitHub/NiTorrent/docs/project-map/ARCH_AUDIT_REPORT.md)

## Summary

Цель плана: закрыть критические расхождения с `TARGET_ARCHITECTURE.md` и `USER_APP_LOGIC.md`, затем снять основные архитектурные долги по состояниям, ошибкам и settings contract, без изменения продуктовой логики вне зафиксированных требований.

## First-Wave Fixes (Закрытие Critical)

### Step C1: Гарантированный deferred replay после engine readiness
- Приоритет: `Critical`
- Цель: обеспечить применение deferred `Start/Pause/Remove` не только на startup restore, но и после переходов readiness/runtime resync.
- Ожидаемый эффект: преддвижковые команды не теряются и догоняются без вмешательства пользователя.
- Затронутые подсистемы: Application workflows, Infrastructure orchestration hooks.
- Критерий готовности:
1. После неуспешного `TryApplyAsync` deferred action остаётся в persistent store.
2. При следующем `engine ready` или `runtime invalidation` запускается replay-попытка.
3. При успешном применении deferred action удаляется из entry.

### Step C2: Intent-first guard в runtime synchronization
- Приоритет: `Critical`
- Цель: исключить ситуацию, когда runtime facts поднимают состояние выше persisted user intent.
- Ожидаемый эффект: `Paused` intent не может неявно стать `Downloading/Seeding` только из-за transient runtime sync.
- Затронутые подсистемы: Domain restore/sync policy, read model projection.
- Критерий готовности:
1. Sync policy учитывает `TorrentIntent` при принятии runtime state.
2. Для `Intent=Paused` проекция остаётся в paused/stopped семантике.
3. Инвариант “runtime refines, not overrides intent” подтверждён тестами.

### Step C3: Надёжный user-facing flow для `open folder`
- Приоритет: `Critical`
- Цель: гарантировать понятное сообщение пользователю при невозможности открытия папки.
- Ожидаемый эффект: ошибка сценария 3.10 не приводит к “тихому” сбою в UI.
- Затронутые подсистемы: Presentation VM error handling, dialog path.
- Критерий готовности:
1. Команда `OpenFolderAsync` в VM обёрнута в `try/catch`.
2. Для ошибок используется `UserErrorMapper` + `ShowTextAsync`.
3. Поведение единообразно с другими командами (`start/pause/remove/add`).

## Prioritized Backlog

### Critical

#### Item C4: Контроль replay-точек и конкуренции deferred execution
- Цель: убрать гонки между replay и runtime sync.
- Ожидаемый эффект: deterministic порядок применения deferred действий.
- Затронутые подсистемы: Application deferred workflow, Infrastructure feed lifecycle.
- Критерий готовности:
1. Replay запускается через единый gate/координатор.
2. Нет двойного применения одного deferred action.
3. Логи содержат outcome каждого replay-цикла.

### Major

#### Item M1: Унифицировать `Paused/Stopped` в user-facing состоянии
- Цель: привести UI/проекцию к одному пользовательскому состоянию.
- Ожидаемый эффект: соответствие ADR/архитектурному правилу unified paused/stopped.
- Затронутые подсистемы: Projection layer, Presentation text/badge mapping.
- Критерий готовности:
1. UI текст и badge для `Paused/Stopped` унифицированы.
2. Командная доступность (`CanStart/CanPause`) остаётся корректной.
3. Проверка сценариев pause/start после рестарта проходит без регрессий.

#### Item M2: Убрать raw runtime exception text из пользовательского UI
- Цель: запретить прямой вывод `manager.Error.ToString()` пользователю.
- Ожидаемый эффект: сообщения соответствуют policy из `FAILURE_MATRIX.md`.
- Затронутые подсистемы: Runtime facts mapping, UI error rendering.
- Критерий готовности:
1. Read model хранит user-safe message или error code.
2. UI не строит текст ошибки из raw exception.
3. Логи сохраняют технические детали отдельно от user-facing сообщения.

#### Item M3: Добавить logging/telemetry в catch-path read synchronization
- Цель: устранить “немые” деградации.
- Ожидаемый эффект: наблюдаемость sync-проблем без ухудшения UX.
- Затронутые подсистемы: Infrastructure read feed.
- Критерий готовности:
1. Catch-path синхронизации пишет warning с контекстом операции.
2. Приложение остаётся в best-effort режиме без крэша.
3. Логи позволяют связать сбой sync с affected cycle/entity.

#### Item M4: Асинхронный shell state query/workflow без `.GetResult()`
- Цель: убрать blocking-вызовы из application shell boundary.
- Ожидаемый эффект: снижение риска deadlock/latency spikes.
- Затронутые подсистемы: Application shell contracts, App close orchestration.
- Критерий готовности:
1. `HandleWindowCloseWorkflow` и shell query работают асинхронно.
2. Вызовы из app-layer адаптированы к async контракту.
3. Поведение close/tray/exit не меняется функционально.

#### Item M5: Реализовать `AskUser` close behavior без fallback на немедленный exit
- Цель: устранить неявный fallback и привести close-flow к явному продуктово-согласованному поведению.
- Ожидаемый эффект: при `AskUser` пользователь действительно выбирает действие, а не получает скрытый full exit.
- Затронутые подсистемы: App close orchestration, shell workflows, dialog policy.
- Критерий готовности:
1. Для `AppShellCloseAction.AskUser` вызывается диалог выбора действия.
2. Результат выбора корректно маппится в `MinimizeToTray` или `ExitApplication`.
3. Нет silent fallback на exit без пользовательского решения.

#### Item M6: Привести страницу темы к единому settings lifecycle (`read/edit/save/apply`)
- Цель: убрать bypass настройки темы мимо общего контракта настроек.
- Ожидаемый эффект: единообразное поведение всех settings-страниц и единый write-path.
- Затронутые подсистемы: Settings UI pages, settings contract wiring.
- Критерий готовности:
1. Theme settings читаются через общий read model подход.
2. Изменения темы проходят через явный save/apply сценарий.
3. Поведение согласовано с остальными настройками страницы/приложения.

#### Item M7: Заменить raw enum-представление статуса в details на user-facing projection
- Цель: убрать технический `Phase.ToString()` из пользовательского экрана деталей.
- Ожидаемый эффект: status в details соответствует пользовательской терминологии и проекции состояний.
- Затронутые подсистемы: Details view-model projection mapping.
- Критерий готовности:
1. Details screen получает статус из user-facing mapper/projection.
2. В UI не отображаются технические enum-значения состояния.
3. Формулировки статуса совпадают с общей моделью состояния в приложении.

#### Item M8: Исправить повреждённые строковые ресурсы (mojibake) в torrent status текстах
- Цель: восстановить корректную локализованную читаемость статусов.
- Ожидаемый эффект: пользователь видит нормальные русские тексты без артефактов кодировки.
- Затронутые подсистемы: Presentation status text resources.
- Критерий готовности:
1. Все статусные строки в `TorrentItemViewModel` читаемы и корректно локализованы.
2. Нет mojibake-символов в runtime/status/error текстах интерфейса.
3. Проверка UI подтверждает корректный рендер в ожидаемой кодировке.

### Minor

#### Item m1: Укрепить settings contract как единственный write-path
- Цель: убрать риск обхода `read/edit/save/apply` через mutable preferences.
- Ожидаемый эффект: единая точка записи настроек.
- Затронутые подсистемы: Application abstractions, Infrastructure settings adapters.
- Критерий готовности:
1. `ITorrentPreferences` ограничен read-only usage или изолирован от write-path.
2. Запись настроек производится через `ITorrentSettingsService`/`ITorrentSettingsRepository`.
3. Settings behavior остаётся единообразным на UI.

## Проверки И Приёмка

### Test Cases (обязательные)

1. Команды до engine readiness:
- Выполнить `Start`, `Pause`, `Remove` до готовности engine.
- Проверить persistence deferred/intents.
- Проверить auto-apply после ready/resync без ручного повторения.

2. Startup/restore:
- Список из каталога появляется до полной инициализации.
- После sync не возникает дублирования, phantom items и потери intent.

3. State mapping:
- `Paused` и `Stopped` отображаются одинаково для пользователя.
- Runtime transitions не нарушают persisted paused intent.

4. Error UX:
- `open folder` при несуществующем пути показывает понятный диалог.
- UI не содержит raw runtime exception text.

5. Close/tray/exit:
- `X` следует настройке close behavior.
- `Tray Exit` всегда выполняет full exit.

### Acceptance Criteria

1. Закрыты все `Critical` пункты отчёта.
2. Матрица section 3 остаётся в статусах без регрессий по implemented сценариям.
3. Логика соответствует `TARGET_ARCHITECTURE.md` sec.5-7 и `APPLICATION_CONTRACTS.md` guarantees.
4. Документация (`CURRENT_ARCHITECTURE_STATE.md`, checklist при необходимости) обновлена в том же change set.
