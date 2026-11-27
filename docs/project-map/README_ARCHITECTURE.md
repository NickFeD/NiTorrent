# Документация архитектуры

Актуализировано 2026-11-27. Этот каталог содержит текущие указатели и прежние решения; архитектура описывается по текущему рабочему дереву.

Начинать с [контекста проекта](../analysis/README.md), [карты кода](../analysis/CURRENT_CODE.md) и [отчёта модульного монолита](../analysis/MODULAR_MONOLITH_IMPLEMENTED_2026-11-27.md). Согласованный объём и ответы владельца — в [ТЗ](../analysis/MODULAR_MONOLITH_REQUIREMENTS.md).

| Документ | Назначение |
| --- | --- |
| [PROJECT_MAP](PROJECT_MAP.md) | Расположение модулей и тестов |
| [TARGET_ARCHITECTURE](TARGET_ARCHITECTURE.md) | Принятый первый этап и допустимое направление развития |
| [APPLICATION_CONTRACTS](APPLICATION_CONTRACTS.md) | Фактический внешний интерфейс Torrents |
| [CURRENT_ARCHITECTURE_STATE](CURRENT_ARCHITECTURE_STATE.md) | Указатель на состояние реализации и проверки |
| [NUGET_PACKAGES_MAP](NUGET_PACKAGES_MAP.md) | Прямые PackageReference из текущих csproj |
| [REGRESSION_CHECKLIST](REGRESSION_CHECKLIST.md) | Проверки будущих изменений |
| [FAILURE_MATRIX](FAILURE_MATRIX.md), [NFR_SLO](NFR_SLO.md) | Текущее поведение и отдельно обозначенные ограничения |
| [OPEN_QUESTIONS](OPEN_QUESTIONS.md), [TECH_DEBT_BACKLOG](TECH_DEBT_BACKLOG.md) | Указатели на открытые вопросы и наблюдения |
| [USER_APP_LOGIC](USER_APP_LOGIC.md), [ADR](ADR/README.md) | Прежние продуктовые требования/решения, не доказательство реализации |
| [archive](archive) | История; записи active/closed внутри снимков относятся к их дате |

Раньше текущими здесь назывались TorrentEntry, intent/deferred workflows и ITorrentWorkflowService. Эти документы больше не используются как карта существующего кода. Они сохранены в архиве, чтобы не переписывать прошлые решения задним числом. Реальное расхождение требований и реализации фиксируется в [OBSERVATIONS](../analysis/OBSERVATIONS.md), а не исправляется молча.
