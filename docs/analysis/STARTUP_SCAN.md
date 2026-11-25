# Статическая проверка пути запуска

Дата: 2026-11-23. Навык: `analyzing-dotnet-performance`, глубина `critical-only`, плюс разбор порядка lifecycle по `systematic-debugging` и `codebase-design`.

Это ограниченная проверка исходников запуска, а не полный аудит проекта. Время выполнения, аллокации и блокировки не измерялись; подтверждённых ускорений нет. Основные выводы и варианты находятся в [STARTUP_LIFECYCLE_DIRECTION.md](STARTUP_LIFECYCLE_DIRECTION.md).

## Область

Полностью прочитаны и проверены 12 файлов:

- `src/NiTorrent.App/Program.cs`
- `src/NiTorrent.App/App.xaml.cs`
- `src/NiTorrent.App/Services/AppStorageService.cs`
- `src/NiTorrent.App/Services/AppLifecycle/NiTorrentApplication.cs`
- `src/NiTorrent.App/Services/AppLifecycle/AppHostLifecycleService.cs`
- `src/NiTorrent.App/Services/AppLifecycle/EarlyLifecycleLogger.cs`
- `src/NiTorrent.App/Services/FileLogger.cs`
- `src/NiTorrent.Application/AppLifecycleCoordinator.cs`
- `src/NiTorrent.Application/Settings/AppSettingsService.cs`
- `src/NiTorrent.Infrastructure/Settings/EngineSettingsService.cs`
- `src/NiTorrent.Infrastructure/Torrents/TorrentEngineCoordinator.cs`
- `src/NiTorrent.Infrastructure/Torrents/JsonTorrentRepository.cs`

Для трассировки зависимостей дополнительно прочитаны DI, runtime lifecycle, задачи запуска/остановки и связанные UI-адаптеры. Числа ниже относятся только к указанным 12 файлам.

## Выполненные проверки до классификации

Автоматический поиск подсчитывал строки с совпадением; это число сигналов, а не автоматически подтверждённых дефектов. Паттерны из `critical-patterns.md` дополнены ручным чтением контекста.

| Проверка | Совпадения / результат |
| --- | --- |
| `.GetAwaiter().GetResult()`, `.Result`, `.Wait()` | 2 строки: ожидание перенаправления вторичного экземпляра в Program и `SemaphoreSlim.Wait` в FileLogger; второе не является ожиданием async Task |
| ValueTask, повторное ожидание ValueTask | 0 упоминаний ValueTask |
| IndexOf со строковым литералом без StringComparison | 0 |
| Substring | 0 |
| StartsWith/EndsWith с одним строковым литералом | 0 |
| Contains с одним строковым литералом | 0 |
| Явные `new byte[...]` | 0 |
| stackalloc, в том числе в циклах | 0 |
| `string.Format` / явный `(object)` | 0; это ограниченный поиск boxing, не анализ всех IL-инструкций |
| Regex / GeneratedRegex, вложенные квантификаторы | 0 |
| ContainsKey перед индексатором | 0 ContainsKey |
| Основные LINQ-операции и материализации | 18 строк; чтением не установлено повторного перечисления одного отложенного внешнего IEnumerable |
| JsonSerializer Serialize/Deserialize | 2 вызова; оба используют source-generated context, 0 из 2 без него |
| Создание JsonSerializerOptions | 0 |
| `new HttpClient` | 0 |
| IndexOfAny / повторное создание набора символов для него | 0 IndexOfAny |
| Классы и sealed | 17 объявлений: 2 static, 15 нестатических, из них 11 sealed; само по себе не основание менять классы |

Отсутствие совпадений не распространяется на код зависимостей и не исключает других причин задержки. LINQ при подготовке списка задач не признан проблемой производительности без измерений. Оптимизированный JSON-context последовательно применяется в обоих вызовах проверенного репозитория; расхождения между ними нет. В составных вызовах отдельно замечены Base64 вместе с копированием `byte[]`, ранние записи логов вместе с разрешением storage и создание lifecycle-графа вместе с I/O в зависимостях.

## Кандидаты на проверку, уровень Info

### P-01. Движок до окна — один порядок запуска

**Влияние:** продолжительность Core входит во время до Shell.
**Места:** `AppLifecycleCoordinator.cs:24`, `TorrentEngineStartupTask.cs:12`, `MainWindowLifecycleTask.cs:11`.
**Направление:** измерить Core и первый кадр; при подтверждении отделить ранний shell от готовности runtime.
**Ограничение:** раннее создание `TorrentMetadataProvider` сейчас требует готового движка.

### P-02. Два пути создания движка — один связанный сценарий

**Влияние:** загрузка настроек может создать runtime до явного старта Core, затем он создаётся ещё раз.
**Места:** `AppSettingsService.cs:26`, `EngineSettingsService.cs:22`, `EngineSettingsService.cs:86`, `TorrentEngineCoordinator.cs:22`, `TorrentEngineCoordinator.cs:33`.
**Направление:** сначала посчитать реальные создания; при подтверждении обеспечить единственного владельца создания runtime и отделить его от применения настроек.
**Ограничение:** вывод статический; не измерено время и не проверено освобождение предыдущего экземпляра.

### P-03. Синхронная запись логов — две реализации

**Влияние:** логирующий поток выполняет файловые операции; основной логгер ещё и ждёт общий gate.
**Места:** `FileLogger.cs:35`, `FileLogger.cs:39`, `EarlyLifecycleLogger.cs:15`, `EarlyLifecycleLogger.cs:18`.
**Направление:** измерить вклад; при подтверждении рассмотреть очередь записи с явным flush и единое разрешение storage.
**Ограничение:** раннюю диагностику нельзя терять при сбое; deadlock здесь не доказан.

### P-04. Побочная работа при разрешении DI — один граф

**Влияние:** построение lifecycle-графа включает создание каталогов и загрузку настроек до StartAsync задач.
**Места:** `AppLifecycleCoordinator.cs:13`, `AppHostLifecycleService.cs:8`, `JsonTorrentRepository.cs:30`, `Infrastructure/DI/DependencyInjection.cs:43`.
**Направление:** разделить время контейнера и конструкторов/фабрик; при подтверждении вынести I/O в явные этапы, откладывать ненужные графы.
**Ограничение:** перенос `Build()` сам по себе не устраняет побочные эффекты разрешения объектов.

### P-05. Источник внутри полного каталога — один формат

**Влияние:** JSON содержит Base64 источника и все файловые записи, которые не обязательно нужны первому экрану.
**Места:** `JsonTorrentRepository.cs:153`, `JsonTorrentRepository.cs:195`, `JsonTorrentRepository.cs:309`.
**Направление:** измерить размер/чтение; при подтверждении сравнить разделение данных и бинарный формат на одинаковых сценариях.
**Ограничение:** размер данных владельца неизвестен; выигрыш MemoryPack не доказан.

| Уровень | Число выводов | Основное |
| --- | --- | --- |
| Critical | 0 подтверждённых | Воспроизведённых deadlock/аварий и измеренных регрессий нет |
| Moderate | 0 подтверждённых | Нет измерений для оценки величины выигрыша |
| Info | 5 кандидатов | Порядок старта, создание движка, логи, DI-граф и состав хранилища |

Результаты статического анализа могут быть неполными. Каждый кандидат требует проверки на реальной конфигурации; классификация не обещает конкретного ускорения.
