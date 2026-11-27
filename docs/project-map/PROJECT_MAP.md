# Карта проекта

Сверено 2026-11-27. Подробные сценарии — [CURRENT_CODE](../analysis/CURRENT_CODE.md), принятые зависимости — [отчёт реализации](../analysis/MODULAR_MONOLITH_IMPLEMENTED_2026-11-27.md).

- [App](../../src/NiTorrent.App): точка входа, host и выбор адаптера.
- [Torrents](../../src/Modules/Torrents): Contracts, Domain, Application, Infrastructure и Engines/MonoTorrent.
- [Desktop](../../src/Modules/Desktop): Contracts, Presentation и WinUI.
- [Updates](../../src/Modules/Updates): Contracts и Windows.
- [tests](../../tests): все пять тестовых проектов. Lifecycle и Architecture — активные; остальные три используют прежнюю модель и не активированы.
- [tools/startup-baseline](../../tools/startup-baseline): изолированные инструменты замеров.
- [NiTorrent.slnx](../../NiTorrent.slnx): фактический состав решения; тип Project отличается от File.

В `src` остались только NiTorrent.App и Modules. Старые каталоги слоёв после переноса удалены. Имена некоторых сборок и namespaces сохранены; владельца типа определять по csproj. Реестр документов — [DOCUMENTATION_STATUS](../DOCUMENTATION_STATUS.md).
