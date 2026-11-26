# Трей H.NotifyIcon и закрытие приложения

Дополнение 2026-11-26: по запросу владельца Win32-меню заменено на [Fluent MenuFlyout в служебном окне](FLUENT_TRAY_MENU_2026-11-26.md). Упоминания нативного PopupMenu ниже описывают предшествующий этап реализации.

Дата: 2026-11-26. Реализовано по [ТЗ](TRAY_REQUIREMENTS.md), после выбора владельцем H.NotifyIcon. [Сравнение библиотек](TRAY_LIBRARY_COMPARISON_2026-11-26.md) сохранено как исследование до реализации.

## Поведение

- Пока основное окно показано, значка нет. При сворачивании значок регистрируется до скрытия окна.
- Двойной левый клик, «Открыть», повторный запуск и внешняя активация возвращают окно и убирают значок. Одиночный левый клик окно не открывает.
- Меню содержит суммарные скорости, «Открыть», «Приостановить всё», «Продолжить всё», «Выход». Скорости берутся из существующих снимков runtime без зависимости от страницы торрентов. Tooltip обновляется по событиям; информационная строка нативного меню — при каждом открытии.
- При первой ошибке регистрации повтор через 500 мс. После второй ошибки окно доступно, диалог предлагает завершение или работу с открытым окном. После «Оставить открытым» следующий пользовательский запрос снова получает до двух попыток.
- `TaskbarCreated` восстанавливает значок через тот же ограниченный сценарий. Реальное завершение Explorer для проверки не выполнялось: сообщение адресно отправлялось служебному окну тестового процесса.
- Крестик поддерживает три режима. Новый профиль или настройки без поля получают «Спрашивать»; явно сохранённые старые значения сохраняются. Диалог поддерживает отмену и запоминание выбора. Настройка доступна как список трёх вариантов.
- «Выход» в трее не спрашивает и использует существующий штатный shutdown. Экономный режим процесса автоматически не включается.
- Обе массовые команды работают со всей сохранённой коллекцией. Пауза не отменяет подготовку; продолжение повторяет ошибочные записи. Удаляемые записи пропускаются. Ошибка одной команды не прекращает обработку остальных, частичная ошибка возвращает окно с сообщением.

## Устройство

`H.NotifyIcon.WinUI` 2.4.1 добавлен в App. Адаптер `TrayService` использует поставляемый через него `H.NotifyIcon.Core.TrayIcon` и `PopupMenu`, а не `TaskbarIcon` с автоматическим восстановлением. Это оставляет одному владельцу NiTorrent контроль над числом попыток и ошибками Explorer, без reflection и изменения сторонних исходников.

`TrayLifecycle` в Presentation содержит переход в трей, ограниченный повтор, отмену при возврате окна и запрет новых переходов после остановки. `MainWindowLifecycle` отвечает за окно, диалоги и настройку закрытия; библиотечные типы через `ITrayService` не проходят.

У H.NotifyIcon регистрация может частично пройти до ошибки, пока `IsCreated` ещё false. `NativeTrayCleanup` удаляет такой значок по GUID через `Shell_NotifyIconW`, затем освобождаются служебное окно и принадлежащий NiTorrent icon handle. Структура следует [NOTIFYICONDATAW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-notifyicondataw); операция удаления — [NIM_DELETE](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw).

`SetAllTorrentStatesUseCase` использует существующую сессию и хранение. Переходы `TorrentDownload.Start/Pause` теперь принимают все живые состояния, включая Created/Failed/Completed: пауза — пользовательское намерение, а завершённая загрузка может продолжить раздачу. Deleted по-прежнему запрещает запуск и паузу. Отдельный общий перезапуск движка не добавлялся.

Исправлен существовавший ранний выход `AppActivationService`: запуск без `.torrent`/magnet теперь тоже показывает окно существующего экземпляра.

WinUIEx остаётся зависимостью для остальных возможностей окна. Создание его TrayIcon больше не используется.

## Автоматические проверки

Полный `NiTorrent.Lifecycle.Tests`: **Passed: 58, Failed: 0, Skipped: 0** в Debug и Release. В эти 58 входят существующие lifecycle-тесты и присутствующие в рабочем дереве тесты выбора файлов; это не 58 новых тестов трея.

| Requirement | Evidence |
| --- | --- |
| Скрывать только после успешной регистрации | `WindowIsHiddenOnlyAfterSuccessfulRegistration` |
| Один повтор и очистка частичного результата | `FailedFirstRegistrationIsCleanedAndRetriedBeforeHiding` |
| Две ошибки, отсутствие скрытия, возможность нового запроса | `TwoFailuresKeepWindowAccessibleAndAllowLaterUserRequest` |
| Отмена при возврате окна и выходе | `ReturningToWindowDuringRetryCancelsStaleRegistration`, `ShutdownDuringRetryPreventsFurtherAttemptsAndNewAdmission` |
| Защита от параллельных переходов и ограниченный recovery | `ConcurrentRequestsShareOneRetrySequence`, `ExplorerRecoveryUsesSameBoundedRetriesAndMarksFailure` |
| Пауза во время подготовки | `PauseAllDuringPreparationPersistsPauseWithoutCancellingPreparation` |
| Продолжение ручных пауз и повтор ошибок | `ResumeAllRestartsManualPausesAndRetriesFailedPreparation` |
| Все живые состояния; сохранение удаления | `EveryLiveTorrentAcceptsBulkPauseAndResume`, `BulkCommandsNeverResurrectPendingDeletion` |
| Изоляция ошибок и отмена массовой команды | `FailureOfOneTorrentDoesNotPreventOtherCommands`, `CancellationDoesNotAdmitBulkCommands` |
| Default и совместимость сохранённого выбора | `NewAndMissingSettingsDefaultToAsk`, `ExplicitLegacyChoiceSurvivesSettingsLoad` |
| Редактор сохраняет три режима и настройки движка | `SettingsEditorSavesAllThreeModesWithoutLosingEngineSettings` |

TRX проверены как XML: `artifacts/tray-comparison/test-results/tray-debug.trx`, `tray-release.trx`. VSTest по-прежнему выводит существующее предупреждение о `VSTEST_ARTIFACTS_DIRECTORY`, результаты сохранены успешно.

Команды из корня, последовательно с build:

```powershell
dotnet build NiTorrent.slnx --no-restore -p:Platform=x64 -p:WindowsPackageType=None -p:PublishAot=false -p:PublishTrimmed=false -v minimal
dotnet test src/NiTorrent.Lifecycle.Tests/NiTorrent.Lifecycle.Tests.csproj --no-restore --no-build --logger 'trx;LogFileName=tray-debug.trx' --results-directory artifacts/tray-comparison/test-results -v minimal
dotnet build NiTorrent.slnx --no-restore -c Release -p:Platform=x64 -p:WindowsPackageType=None -p:PublishAot=false -p:PublishTrimmed=false -v minimal
dotnet test src/NiTorrent.Lifecycle.Tests/NiTorrent.Lifecycle.Tests.csproj --no-restore --no-build -c Release --logger 'trx;LogFileName=tray-release.trx' --results-directory artifacts/tray-comparison/test-results -v minimal
```

Managed x64 Debug и Release собраны успешно. В Release остаются три существующих nullable-предупреждения в PreviewTorrentContentsCommand, TorrentPeerItemViewModel и TorrentTrackerItemViewModel. Дополнительные итоговые App builds после защиты меню записаны в `build-debug-final.log` и `build-release-final.log` в том же каталоге artifacts.

## Настоящее окно и ограничения

Все диагностические запуски использовали `NITORRENT_DATA_ROOT` с отдельным каталогом, один синтетический торрент с одним байтом данных, без DHT, port forwarding и local peer discovery. Профиль пользователя не использовался. Диагностические процессы завершены.

Проверочные helpers: `artifacts/tray-comparison/ui-tray-smoke.ps1`, `ui-dialog-smoke.ps1`. Запуск вне песочницы потребовался для настоящего значка: в песочнице native-регистрация возвращала отказ. Этот отказ отдельно использован для проверки диалога ошибки. Нативное меню проверялось через его Win32-пункты и адресные сообщения тестовому окну; глобальный ввод пользователя не отправлялся.

Подтверждённые отдельные прогоны:

| Прогон | Доказательство |
| --- | --- |
| Debug: значок, recovery, двойной клик, меню, «Выход» | `artifacts/startup-baseline/ui-Debug-20261125T230013731Z/profile`, ExitCode=0, Clean |
| Debug: отмена вопроса, сворачивание без запоминания, сохранение выбора, применение сохранённого режима | `artifacts/startup-baseline/ui-Debug-20261125T230154876Z/profile`, ExitCode=0 |
| Debug в песочнице: две ошибки, «Оставить открытым», новый запрос и «Завершить приложение» | `artifacts/startup-baseline/ui-Debug-20261125T230231765Z/profile`, ExitCode=0 |
| Release: значок, одиночный и двойной клик, recovery, повторный запуск, команды продолжения/паузы из меню с записью Running/Paused, «Выход» | `artifacts/startup-baseline/ui-Release-20261125T230539433Z/profile`, ExitCode=0, Clean |
| Итоговый Release после защиты от повторного открытия меню: тот же полный сценарий | `artifacts/startup-baseline/ui-Release-20261125T231429946Z/profile`, ExitCode=0, Clean |

Имена каталогов artifacts содержат UTC-дату 25 ноября; локальная дата проверки в Москве — 26 ноября.

В промежуточных диагностических прогонах чтение каталога через PowerShell `Get-Content` могло блокировать его атомарную замену: команда меняла намерение в памяти, но пробный скрипт ждал немедленного изменения файла и завершал процесс по таймауту. Итоговый helper открывает каталог с `FileShare.ReadWrite | FileShare.Delete`. После исправления диагностики обе команды и штатное сохранение проверены успешно; поведение production-хранилища для этого не менялось.

Не проверены Native AOT/trimming, MSIX и установка, x86/ARM64, физическое перезапускание Explorer, реальные установленные ассоциации `.torrent`/magnet, взаимодействие с открытым предпросмотром и долговременная передача на рабочей коллекции. Живое изменение строки уже открытого нативного меню не реализовано; свежие значения появляются при следующем открытии. Исследование энергопотребления и расширенная панель остаются отдельными задачами.

Коммит и публикация не выполнялись. Существующие изменения предпросмотра, дерева файлов, темы и логирования сохранены.
