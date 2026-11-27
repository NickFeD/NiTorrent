# NiTorrent

Windows-клиент на WinUI и .NET 11. Состояние документации сверено с рабочим деревом 27 ноября 2026 года.

- [Начало работы и порядок чтения](docs/analysis/README.md).
- [Текущая карта кода](docs/analysis/CURRENT_CODE.md).
- [Модульный монолит: реализация, проверки и ограничения](docs/analysis/MODULAR_MONOLITH_IMPLEMENTED_2026-11-27.md).
- [Согласованные требования](docs/analysis/MODULAR_MONOLITH_REQUIREMENTS.md).
- [Актуальность всех Markdown-документов](docs/DOCUMENTATION_STATUS.md).
- [Открытые вопросы](docs/analysis/QUESTIONS.md) и [понятия проекта](GLOSSARY.md).

Исходники находятся в `src/NiTorrent.App` и `src/Modules/{Torrents,Desktop,Updates}`, все пять тестовых проектов — в `tests`. Активны Lifecycle и Architecture; три тестовых проекта прежней модели сохранены без включения в сборку решения.

Для managed x64-проверки на Windows с SDK из [global.json](global.json) и Windows SDK:

```powershell
dotnet build NiTorrent.slnx -p:Platform=x64 -p:WindowsPackageType=None -p:PublishAot=false -p:PublishTrimmed=false -p:PublishReadyToRun=false -v minimal
dotnet test NiTorrent.slnx --no-build --no-restore -p:Platform=x64 -p:WindowsPackageType=None -p:PublishAot=false -p:PublishTrimmed=false -p:PublishReadyToRun=false --logger trx --results-directory artifacts/verification -v minimal
```

Для Release добавить `-c Release` в обе команды. Последние результаты — 73 lifecycle + 13 архитектурных тестов; это зафиксированный прогон, а не гарантия состояния после будущих изменений. AOT/MSIX и x86/ARM64 этими проверками не подтверждены. Подробные команды и журналы находятся в отчёте реализации.
