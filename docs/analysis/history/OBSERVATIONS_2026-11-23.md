# Наблюдения и расхождения

Дата: 2026-11-23. Все пункты получены статическим чтением. Здесь нет результатов воспроизведения, измерений или утверждений об успешной сборке. Предложенный порядок проверки не является разрешением менять реализацию.

## Подтверждено исходниками

| ID | Наблюдение и доказательство | Практическое значение |
| --- | --- | --- |
| O-01 | [CURRENT_ARCHITECTURE_STATE](../project-map/CURRENT_ARCHITECTURE_STATE.md) описывает `TorrentEntry`, intent/deferred и workflow-контракты. Текущие [Domain](../../src/NiTorrent.Domain/Torrents/TorrentDownload.cs) и [use cases](../../src/NiTorrent.Application/Torrents/UseCase/CreateTorrentDownloadUseCase.cs) используют другую модель. | Нельзя строить план изменений только по прежней архитектурной карте. |
| O-02 | [Application-тест](../../src/NiTorrent.Application.Tests/Torrents/AddTorrentUseCaseTests.cs) использует прежние `AddTorrentUseCase`/`TorrentEntry`; [Infrastructure-тест](../../src/NiTorrent.Infrastructure.Tests/Torrents/EngineBackedTorrentReadModelFeedTests.cs) — прежний read feed. Определений этих production-типов в текущем дереве `src` не найдено. Domain-тесты тоже используют прежние типы. | Тестовые исходники нельзя считать актуальной защитой текущих сценариев. Ошибки компиляции здесь предполагаются по ссылкам; фактический запуск не выполнялся. |
| O-03 | В [NiTorrent.slnx](../../NiTorrent.slnx) три тестовых `.csproj` перечислены как `File`, тогда как основные проекты перечислены как `Project`. В [release.yml](../../.github/workflows/release.yml) нет шага тестирования. | Наличие файлов в Solution Explorer не подтверждает участие тестовых проектов в сборке решения. Это описание конфигурации, без её исправления. |
| O-04 | В [TorrentRuntimeGateway.RemoveAsync](../../src/NiTorrent.Infrastructure/Torrents/TorrentRuntimeGateway.cs) аргумент `deleteFiles` не используется: выполняются Stop и Remove менеджера. | Выбор режима удаления не передаётся в отдельную операцию удаления скачанных данных в этом адаптере. Поведение библиотеки на диске не проверялось. |
| O-05 | В [TorrentDetailsViewModel](../../src/NiTorrent.Presentation/Features/Torrents/TorrentDetailsViewModel.cs) `LoadAsync` сбрасывает состояние, а заполнение данных закомментировано. `RefreshRuntimeAsync` не содержит активной логики получения данных; `_currentTorrentId` присваивается только `Guid.Empty`. | Описание полностью работающих деталей, peers и графика в старом отчёте нельзя переносить на эту реализацию. |
| O-06 | [JsonTorrentRepository.SaveUnsafeAsync](../../src/NiTorrent.Infrastructure/Torrents/JsonTorrentRepository.cs) использует `File.Create` целевого JSON; загрузка напрямую десериализует файл. В этих методах нет временного файла с заменой, резервной копии или восстановления испорченного JSON. | Сбой записи или повреждение хранилища требуют отдельного сценария проверки и решения о восстановлении. |
| O-07 | [CreateTorrentDownloadUseCase](../../src/NiTorrent.Application/Torrents/UseCase/CreateTorrentDownloadUseCase.cs) сохраняет запись до runtime Add/Start и не содержит компенсации. [Start](../../src/NiTorrent.Application/Torrents/UseCase/StartTorrentUseCase.cs)/[Pause](../../src/NiTorrent.Application/Torrents/UseCase/PauseTorrentUseCase.cs) обращаются к runtime перед сохранением нового статуса. | Ошибка между шагами может оставить разные состояния хранения и движка. Желаемый результат при частичном успехе ещё нужно определить. |
| O-08 | `TorrentDownload.Size` существует, но в [TorrentDownloadRecord и преобразованиях](../../src/NiTorrent.Infrastructure/Torrents/JsonTorrentRepository.cs) это поле отсутствует. | Сохранение и повторное чтение этой моделью не сохраняют `Size`; влияние на конкретное отображение требует проверки UI. |
| O-09 | [MainWindowLifecycle](../../src/NiTorrent.App/Services/AppLifecycle/MainWindowLifecycle.cs) для `AppCloseBehavior.AskUser` сразу вызывает `RequestShutdown`, без диалога в этой ветке. | Название режима и поведение этой ветки расходятся; нужно выяснить, должен ли режим вообще оставаться. |

## Требуют отдельной проверки

### Команды во время восстановления

[USER_APP_LOGIC, раздел 4](../project-map/USER_APP_LOGIC.md) требует сохранять команды до готовности движка. В текущих use cases нет прежней модели deferred actions, gateway напрямую получает менеджер по Id. При этом окно показывается на Shell до Restore, а `TorrentViewModel` также может запросить восстановление.

Нужно проверить сценарий: сохранённый торрент виден, но его runtime-менеджер ещё не добавлен; пользователь нажимает Start/Pause/Delete. Само существование такой гонки в UI не воспроизведено. Отсутствие очереди не означает, что нужно автоматически вернуть прежнюю архитектуру.

### Повторное добавление и magnet metadata

Preview и Create отдельно вызывают извлечение metadata; для magnet это два входа в путь получения metadata, хотя библиотека может использовать кэш. Проверка дубликата по InfoHash отдельно предшествует Add; внутри репозитория уникальность добавления проверяется по Id.

Нужно отдельно проверить повторное и конкурентное добавление одного InfoHash. Сетевые издержки повторного извлечения здесь не измерены.

### Жизненный цикл подписок

В [TorrentViewModel.TorrentUnloaded](../../src/NiTorrent.Presentation/Features/Torrents/TorrentViewModel.cs) отписка ожидается синхронно через `GetAwaiter().GetResult()`, тогда как обработчик обновления использует dispatcher. У runtime-источника есть async multicast-обработчики и общие коллекции.

Это повод проверить навигацию и завершение во время обновлений. Блокировка, гонка или потеря события в этом анализе не воспроизведены.

### Применение настроек

[UpdateSettingsUseCase](../../src/NiTorrent.Application/Settings/UpdateSettingsUseCase.cs) обновляет `Current` до применения настроек к движку; [SettingsRepository.SaveAsync](../../src/NiTorrent.Infrastructure/Settings/SettingsRepository.cs) не ожидает вновь созданную задачу записи. Требуется определить, что пользователь должен увидеть при ошибке сохранения или применения и когда Apply считается завершённым.

## Рекомендуемый порядок следующего исследования

1. Выбрать один пользовательский сценарий и уточнить результат по [QUESTIONS.md](QUESTIONS.md).
2. Проверить сборку актуальных проектов и состояние существующих тестов в отдельной задаче.
3. Для надёжности коллекции исследовать запись/восстановление и команды при запуске: O-06, O-07.
4. Если задача про удаление или экран деталей, начать с O-04 либо O-05.
5. После реализации обновить затронутые описания, чтобы снова не получить две несовместимые карты проекта.

Ни один пункт в этом списке не реализован в рамках анализа.
