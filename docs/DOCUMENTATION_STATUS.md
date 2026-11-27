# Реестр документации

Проверено 2026-11-27. Область: все Markdown-файлы репозитория, которые возвращает `rg --files -g '*.md' -g '!artifacts/**'`. Генерируемые артефакты, каталоги сборки и игнорируемые служебные файлы не являются документацией проекта и в реестр не входят.

## Как читать документы

Текущий контекст: [карта кода](analysis/CURRENT_CODE.md), [отчёт первого этапа](analysis/MODULAR_MONOLITH_IMPLEMENTED_2026-11-27.md), [ТЗ](analysis/MODULAR_MONOLITH_REQUIREMENTS.md), [вопросы](analysis/QUESTIONS.md) и [наблюдения](analysis/OBSERVATIONS.md). Навигация начинается с [README анализа](analysis/README.md). Обновлённые документы project-map описывают текущие модули и ссылаются на этот контекст.

Поздние согласованные требования определяют объём этапа. Прежние продуктовые спецификации и принятые ADR сохраняют происхождение решений, но не доказывают наличие функции в коде. Замеченное расхождение о показе Paused/Stopped записано в наблюдениях; решение владельца не изменялось.

Исторические планы и замеры сохранены с явным статусом. Старые пути к удалённому коду представлены как исторические пути; рабочие ссылки на сохранившийся код приведены к текущему расположению. Прежние версии переписанных архитектурных карт находятся в `project-map/archive/`. Снимки анализа от 23 ноября — в `analysis/history/`.

## Что актуализировано

- Границы Torrents/Desktop/Updates/App, 11 production-проектов, явный выбор адаптера MonoTorrent и подписки без истории.
- Папка `tests/`: два активных и три исторических проекта; последние зафиксированные результаты — 73 lifecycle + 13 архитектурных тестов. Эти результаты предшествуют текущей правке документов и не удостоверяют последующие изменения рабочего дерева.
- Карта прямых NuGet-зависимостей сверена с текущими csproj, без поиска новых версий в сети.
- Вопросы и ограничения: подробные наблюдения, второй движок, ошибки хранения, отсутствие транзакционного отката настроек, прежние ожидания от предпросмотра и восстановления.
- Lifecycle-требования отделены от уже выполненных решений; инструкции измерителя и справка значков уточнены.

Локальные файловые ссылки проверяются по существованию целей. Внешние URL, корректность Markdown-якорей, работа страниц GitHub и актуальность внешних версий отдельно не проверялись. Сборка и тесты при этой правке документации не запускались; приложение не изменялось. Исходники и тесты, уже изменённые другим действием в рабочем дереве, оставлены как есть. Handoff сохраняется во временной папке ОС согласно навыку `handoff`.

## Полный перечень

После описанной выше проверки документации отдельным согласованным изменением 27 ноября обновлён предпросмотр торрента. Актуальное поведение и результаты 76 lifecycle + 13 architecture тестов приведены в [CURRENT_CODE](analysis/CURRENT_CODE.md#добавление-и-управление); README анализа, QUESTIONS и OBSERVATIONS дополнены. Утверждения выше о проходе только по документации относятся к предшествующей актуализации.
Всего файлов: **85**.

| Документ | Статус |
| --- | --- |
| [docs/analysis/CURRENT_CODE.md](analysis/CURRENT_CODE.md) | Текущий справочный документ; проверен |
| [docs/analysis/DEPENDENCY_UPGRADE_2026-11-25.md](analysis/DEPENDENCY_UPGRADE_2026-11-25.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/ENGINE_CANDIDATES_2026-11-26.md](analysis/ENGINE_CANDIDATES_2026-11-26.md) | Исследование кандидатов; выбор второго движка не сделан |
| [docs/analysis/FLUENT_TRAY_MENU_2026-11-26.md](analysis/FLUENT_TRAY_MENU_2026-11-26.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/history/CURRENT_CODE_2026-11-23.md](analysis/history/CURRENT_CODE_2026-11-23.md) | Исторический снимок; добавлена отметка статуса |
| [docs/analysis/history/OBSERVATIONS_2026-11-23.md](analysis/history/OBSERVATIONS_2026-11-23.md) | Исторический снимок; добавлена отметка статуса |
| [docs/analysis/LIFECYCLE_REQUIREMENTS.md](analysis/LIFECYCLE_REQUIREMENTS.md) | Согласованные требования; ограничения реализации см. CURRENT_CODE |
| [docs/analysis/MODULAR_MONOLITH_IMPLEMENTED_2026-11-27.md](analysis/MODULAR_MONOLITH_IMPLEMENTED_2026-11-27.md) | Отчёт завершённого этапа; результаты относятся к указанным прогонам |
| [docs/analysis/MODULAR_MONOLITH_REQUIREMENTS.md](analysis/MODULAR_MONOLITH_REQUIREMENTS.md) | Согласованные требования; ограничения реализации см. CURRENT_CODE |
| [docs/analysis/OBSERVATIONS.md](analysis/OBSERVATIONS.md) | Текущий справочный документ; проверен |
| [docs/analysis/QUESTIONS.md](analysis/QUESTIONS.md) | Текущий справочный документ; проверен |
| [docs/analysis/README.md](analysis/README.md) | Текущий справочный документ; проверен |
| [docs/analysis/RUNTIME_LIFECYCLE_IMPLEMENTED.md](analysis/RUNTIME_LIFECYCLE_IMPLEMENTED.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/STARTUP_BASELINE_2026-11-24.md](analysis/STARTUP_BASELINE_2026-11-24.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/STARTUP_CALL_PIPELINE.md](analysis/STARTUP_CALL_PIPELINE.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/STARTUP_LIFECYCLE_DIRECTION.md](analysis/STARTUP_LIFECYCLE_DIRECTION.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/STARTUP_SCAN.md](analysis/STARTUP_SCAN.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/TRAY_IMPLEMENTED_2026-11-26.md](analysis/TRAY_IMPLEMENTED_2026-11-26.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/TRAY_LIBRARY_COMPARISON_2026-11-26.md](analysis/TRAY_LIBRARY_COMPARISON_2026-11-26.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/analysis/TRAY_REQUIREMENTS.md](analysis/TRAY_REQUIREMENTS.md) | Согласованные требования; ограничения реализации см. CURRENT_CODE |
| [docs/analysis/WORKING_GUIDE.md](analysis/WORKING_GUIDE.md) | Текущий справочный документ; проверен |
| [docs/DOCUMENTATION_STATUS.md](DOCUMENTATION_STATUS.md) | Текущий справочный документ; проверен |
| [docs/project-map/ADR/ADR-001-unified-magnet-torrent-add-flow.md](project-map/ADR/ADR-001-unified-magnet-torrent-add-flow.md) | Принятое ADR; сохранено решение, не подтверждение реализации |
| [docs/project-map/ADR/ADR-002-unified-paused-stopped-ui-state.md](project-map/ADR/ADR-002-unified-paused-stopped-ui-state.md) | Принятое ADR; сохранено решение, не подтверждение реализации |
| [docs/project-map/ADR/ADR-003-remove-mode-selection-and-quick-remove.md](project-map/ADR/ADR-003-remove-mode-selection-and-quick-remove.md) | Принятое ADR; сохранено решение, не подтверждение реализации |
| [docs/project-map/ADR/ADR-004-app-owned-startup-restore-no-engine-wide-restore.md](project-map/ADR/ADR-004-app-owned-startup-restore-no-engine-wide-restore.md) | Принятое ADR; сохранено решение, не подтверждение реализации |
| [docs/project-map/ADR/README.md](project-map/ADR/README.md) | Индекс ADR; уточнена связь с текущей реализацией |
| [docs/project-map/ANTI_PATTERNS.md](project-map/ANTI_PATTERNS.md) | Текущий справочный документ; проверен |
| [docs/project-map/APPLICATION_CONTRACTS.md](project-map/APPLICATION_CONTRACTS.md) | Текущий справочный документ; проверен |
| [docs/project-map/ARCH_AUDIT_REPORT.md](project-map/ARCH_AUDIT_REPORT.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/project-map/ARCH_REMEDIATION_PLAN.md](project-map/ARCH_REMEDIATION_PLAN.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/project-map/archive/APPLICATION_CONTRACTS_2026-04-06.md](project-map/archive/APPLICATION_CONTRACTS_2026-04-06.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/ARCHITECTURE_TRANSITION_PLAN.md](project-map/archive/ARCHITECTURE_TRANSITION_PLAN.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/CHANGELOG_ARCHITECTURE_REWRITE.md](project-map/archive/CHANGELOG_ARCHITECTURE_REWRITE.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/CURRENT_ARCHITECTURE_STATE_2026-04-10.md](project-map/archive/CURRENT_ARCHITECTURE_STATE_2026-04-10.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/FAILURE_MATRIX_2026-04-06.md](project-map/archive/FAILURE_MATRIX_2026-04-06.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/NFR_SLO_2026-04-05.md](project-map/archive/NFR_SLO_2026-04-05.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/NUGET_PACKAGES_MAP_before-2026-11-27.md](project-map/archive/NUGET_PACKAGES_MAP_before-2026-11-27.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE1_DOMAIN_IMPLEMENTATION.md](project-map/archive/PHASE1_DOMAIN_IMPLEMENTATION.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE10_DETAILS_SLICE.md](project-map/archive/PHASE10_DETAILS_SLICE.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE11_DETAILS_WIRING.md](project-map/archive/PHASE11_DETAILS_WIRING.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE12_PER_TORRENT_RUNTIME_SETTINGS.md](project-map/archive/PHASE12_PER_TORRENT_RUNTIME_SETTINGS.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE13_UI_LEGACY_DECOUPLING.md](project-map/archive/PHASE13_UI_LEGACY_DECOUPLING.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE14_WRITE_PATH_DECOUPLING.md](project-map/archive/PHASE14_WRITE_PATH_DECOUPLING.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE15_LEGACY_ADAPTER_ISOLATION.md](project-map/archive/PHASE15_LEGACY_ADAPTER_ISOLATION.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE16_ENGINE_OWNED_READ_STATUS.md](project-map/archive/PHASE16_ENGINE_OWNED_READ_STATUS.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE17_ENGINE_OWNED_WRITE_PATH.md](project-map/archive/PHASE17_ENGINE_OWNED_WRITE_PATH.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE18_THIN_COMPATIBILITY_LAYER.md](project-map/archive/PHASE18_THIN_COMPATIBILITY_LAYER.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE19_REMOVE_LEGACY_SERVICE.md](project-map/archive/PHASE19_REMOVE_LEGACY_SERVICE.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE2_PRODUCT_REPOSITORY.md](project-map/archive/PHASE2_PRODUCT_REPOSITORY.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE3_ENGINE_GATEWAY.md](project-map/archive/PHASE3_ENGINE_GATEWAY.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE4_RESTORE_WORKFLOW.md](project-map/archive/PHASE4_RESTORE_WORKFLOW.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE5_COMMANDS.md](project-map/archive/PHASE5_COMMANDS.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE6_DEFERRED_ACTIONS.md](project-map/archive/PHASE6_DEFERRED_ACTIONS.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE7_READ_SIDE.md](project-map/archive/PHASE7_READ_SIDE.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE8_SETTINGS_SUBSYSTEM.md](project-map/archive/PHASE8_SETTINGS_SUBSYSTEM.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/PHASE9_SHELL_POLICY.md](project-map/archive/PHASE9_SHELL_POLICY.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/POST_TEST_REFACTOR_PLAN.md](project-map/archive/POST_TEST_REFACTOR_PLAN.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/REFORM_PLAN.md](project-map/archive/REFORM_PLAN.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/REFORM_STATUS_REPORT.md](project-map/archive/REFORM_STATUS_REPORT.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/REGRESSION_CHECKLIST.md](project-map/archive/REGRESSION_CHECKLIST.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/REGRESSION_CHECKLIST_2026-04-06.md](project-map/archive/REGRESSION_CHECKLIST_2026-04-06.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/TARGET_ARCHITECTURE.md](project-map/archive/TARGET_ARCHITECTURE.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/TARGET_ARCHITECTURE_2026-04-06.md](project-map/archive/TARGET_ARCHITECTURE_2026-04-06.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/TARGET_ARCHITECTURE_V2.md](project-map/archive/TARGET_ARCHITECTURE_V2.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/TECH_DEBT_BACKLOG.md](project-map/archive/TECH_DEBT_BACKLOG.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/archive/TRANSITION_BACKLOG.md](project-map/archive/TRANSITION_BACKLOG.md) | Исторический снимок; добавлена отметка статуса |
| [docs/project-map/CURRENT_ARCHITECTURE_STATE.md](project-map/CURRENT_ARCHITECTURE_STATE.md) | Текущий справочный документ; проверен |
| [docs/project-map/FAILURE_MATRIX.md](project-map/FAILURE_MATRIX.md) | Текущий справочный документ; проверен |
| [docs/project-map/NFR_SLO.md](project-map/NFR_SLO.md) | Текущий справочный документ; проверен |
| [docs/project-map/NUGET_PACKAGES_MAP.md](project-map/NUGET_PACKAGES_MAP.md) | Текущий справочный документ; проверен |
| [docs/project-map/ONBOARDING.md](project-map/ONBOARDING.md) | Текущий справочный документ; проверен |
| [docs/project-map/OPEN_QUESTIONS.md](project-map/OPEN_QUESTIONS.md) | Текущий справочный документ; проверен |
| [docs/project-map/PROJECT_MAP.md](project-map/PROJECT_MAP.md) | Текущий справочный документ; проверен |
| [docs/project-map/README_ARCHITECTURE.md](project-map/README_ARCHITECTURE.md) | Текущий справочный документ; проверен |
| [docs/project-map/REGRESSION_CHECKLIST.md](project-map/REGRESSION_CHECKLIST.md) | Текущий справочный документ; проверен |
| [docs/project-map/STARTUP_RESTORE_REDESIGN_PLAN.md](project-map/STARTUP_RESTORE_REDESIGN_PLAN.md) | Датированный отчёт/план; сохранён с исторической отметкой |
| [docs/project-map/TARGET_ARCHITECTURE.md](project-map/TARGET_ARCHITECTURE.md) | Текущий справочный документ; проверен |
| [docs/project-map/TECH_DEBT_BACKLOG.md](project-map/TECH_DEBT_BACKLOG.md) | Текущий справочный документ; проверен |
| [docs/project-map/USER_APP_LOGIC.md](project-map/USER_APP_LOGIC.md) | Прежняя продуктовая спецификация; отмечен приоритет поздних требований |
| [GLOSSARY.md](../GLOSSARY.md) | Доменный словарь; проверен, изменения не требуются |
| [README.md](../README.md) | Текущий справочный документ; проверен |
| [src/NiTorrent.App/Assets/FileTypes/README.md](../src/NiTorrent.App/Assets/FileTypes/README.md) | Справка ресурсов; сверена с SVG и текущим сопоставлением |
| [src/NiTorrent.App/Assets/Icons/README.md](../src/NiTorrent.App/Assets/Icons/README.md) | Справка ресурсов; сверена с SVG и текущим сопоставлением |
| [tools/startup-baseline/README.md](../tools/startup-baseline/README.md) | Инструкция инструмента; уточнены актуальный runtime и границы замеров |
