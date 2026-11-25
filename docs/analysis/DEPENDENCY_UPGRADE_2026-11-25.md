# Обновление зависимостей — 2026-11-25

Проект переведён с .NET 10 на .NET 11. SDK закреплён в `global.json`: 11.0.100, без preview. Все проекты `src` используют `net11.0`; приложение — `net11.0-windows10.0.26100.0`. Windows TargetPlatformMinVersion сохранён. Release workflow использует SDK 11; инструмент storage baseline по умолчанию тоже использует .NET 11.

## Версии

Версии выбраны по стабильным выпускам NuGet. Повторный `dotnet list NiTorrent.slnx package --outdated --format json` не нашёл устаревших прямых зависимостей в шести проектах решения. Снимки до изменений и отчёты находятся в `artifacts/dependency-upgrade/`.

| Зависимость | До | После |
| --- | --- | --- |
| DevWinUI / ContextMenu | 10.0.0 | 11.1.0 |
| DevWinUI.SourceGenerator | 10.0.0 | 10.9.0 |
| Microsoft.Extensions.* | 10.0.9 | 11.0.0 |
| WindowsAppSDK | 2.2.0 | 2.7.0 |
| CsWinRT | 2.2.0 | 2.3.1 |
| Windows.SDK.BuildTools | 10.0.28000.1839 | 10.0.28000.2957 |
| WinUIEx | 2.9.1 | 2.9.3 |
| nucs.JsonSettings | 2.0.2 | 2.3.2 |
| Microsoft.NET.Test.Sdk | см. снимки проектов | 18.12.0 |
| xunit | см. снимки проектов | 2.9.3 |
| xunit.runner.visualstudio | см. снимки проектов | 4.0.1 |
| coverlet.collector (старые тестовые проекты) | см. снимки проектов | 10.1.1 |

MonoTorrent 3.0.2, CommunityToolkit 8.4.2, Newtonsoft.Json 13.0.4, Behaviors 3.0.1 и AutoSaveGenerator 2.0.4 уже актуальны. Различие версий DevWinUI и его SourceGenerator намеренное: это отдельные пакеты. Удалены четыре явные ссылки на Logging.Abstractions / DependencyInjection.Abstractions: в .NET 11 эти сборки доступны в reference pack; NuGet выдавал NU1510.

## Проверка

Все команды выполнялись из корня репозитория. Сборки и тесты, использующие общие выходные каталоги, выполнялись последовательно.

- Restore решения с доступом к NuGet завершился успешно, без предупреждений.
- Отдельный restore трёх старых тестовых проектов также успешен; их исходники не собирались и тесты не запускались.
- Debug x64: успешная managed-сборка, 0 ошибок, 7 предупреждений. Лог: `artifacts/dependency-upgrade/build-debug-clean.log`.
- Debug lifecycle tests: Passed: 29, Failed: 0, Skipped: 0. TRX: `artifacts/dependency-upgrade/test-results/net11-debug-final.trx`.
- Debug UI: отдельный профиль, один синтетический торрент в паузе, сетевое обнаружение отключено. Окно показано, торрент восстановлен, закрытие штатное, ExitCode=0, shutdown marker `Clean`. Образец: `artifacts/startup-baseline/ui-Debug-20261125T172535951Z/sample.json`.
- Release x64: успешная managed-сборка, 0 ошибок, 7 предупреждений. Лог: `artifacts/dependency-upgrade/build-release.log`.
- Release lifecycle tests: Passed: 29, Failed: 0, Skipped: 0. TRX: `artifacts/dependency-upgrade/test-results/net11-release.trx`.
- Release UI: тот же изолированный сценарий, окно показано, торрент восстановлен в паузе, ExitCode=0, shutdown marker `Clean`. Образец: `artifacts/startup-baseline/ui-Release-20261125T172738359Z/sample.json`.

Команды сборки: `dotnet build NiTorrent.slnx --no-restore [-c Release] -p:Platform=x64 -p:WindowsPackageType=None -p:PublishAot=false -p:PublishTrimmed=false -v minimal`.

Команды тестов: `dotnet test src/NiTorrent.Lifecycle.Tests/NiTorrent.Lifecycle.Tests.csproj --no-restore --no-build [-c Release] --logger 'trx;LogFileName=<имя>.trx' --results-directory artifacts/dependency-upgrade/test-results -v minimal`. Это xUnit v2 через VSTest, не MTP.

## Ограничения

- WinUIEx 2.9.3 по-прежнему выдаёт `SetShownInSwitchers failed` при создании TrayIcon. Ошибка изолирована; ядро продолжает работу. Нативный трей требует отдельного исследования. Обновление библиотек не решило эту проблему.
- Остались предупреждения nullable, XAML bindings и AOT-аннотаций. Проверялись managed-сборки, не Native AOT, trimming, MSIX или установка.
- Test runner после успешных тестов сообщает об отсутствии `VSTEST_ARTIFACTS_DIRECTORY` и использует временный каталог. Пакет Test SDK уже обновлён; TRX сохранён по указанному пути.
- При повторной сборке с `--no-incremental` XAML-компилятор потерял сгенерированный код окна предпросмотра. `dotnet clean` и обычная сборка прошли успешно; исходный XAML для обхода не менялся. Лог неудачной попытки: `build-debug-final.log`.
- Три старых тестовых проекта в решении представлены как файлы, их тесты не входят в результат 29/29 и содержат ранее известные несовместимости с текущим кодом.
- Один запуск каждой конфигурации с синтетическим профилем не доказывает ускорение. Видимость окна: Debug около 5,53 с, Release около 6,31 с; кэш и время первого отрисованного кадра не контролировались. Сравнение с реальным профилем некорректно.
- Пользовательские изменения UI и ресурсов сохранены. Коммит и публикация не выполнялись.

Описание реализованного жизненного цикла: [RUNTIME_LIFECYCLE_IMPLEMENTED.md](RUNTIME_LIFECYCLE_IMPLEMENTED.md). Продуктовые договорённости: [LIFECYCLE_REQUIREMENTS.md](LIFECYCLE_REQUIREMENTS.md).
