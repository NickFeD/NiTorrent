# Прямые зависимости проектов

Сверено 2026-11-27 по текущим csproj и global.json. Здесь указаны прямые PackageReference, а не полный транзитивный граф, версии установленного runtime или рекомендации обновить библиотеки. Сеть и наличие новых выпусков не проверялись.

SDK закреплён в [global.json](../../global.json): 11.0.100. Роли модулей и ограничения зависимостей — [CURRENT_CODE](../analysis/CURRENT_CODE.md). Предыдущая карта с общими рекомендациями сохранена в [архиве](archive/NUGET_PACKAGES_MAP_before-2026-11-27.md).

| Проект | TargetFramework в исходнике | Роль |
| --- | --- | --- |
| [NiTorrent.Desktop.Contracts](../../src/Modules/Desktop/Contracts/NiTorrent.Desktop.Contracts.csproj) | `net11.0` | Production |
| [NiTorrent.Presentation](../../src/Modules/Desktop/Presentation/NiTorrent.Presentation.csproj) | `net11.0` | Production |
| [NiTorrent.Desktop.WinUI](../../src/Modules/Desktop/WinUI/NiTorrent.Desktop.WinUI.csproj) | `net11.0-windows10.0.26100.0` | Production |
| [NiTorrent.Application](../../src/Modules/Torrents/Application/NiTorrent.Application.csproj) | `net11.0` | Production |
| [NiTorrent.Torrents.Contracts](../../src/Modules/Torrents/Contracts/NiTorrent.Torrents.Contracts.csproj) | `net11.0` | Production |
| [NiTorrent.Domain](../../src/Modules/Torrents/Domain/NiTorrent.Domain.csproj) | `net11.0` | Production |
| [NiTorrent.Torrents.MonoTorrent](../../src/Modules/Torrents/Engines/MonoTorrent/NiTorrent.Torrents.MonoTorrent.csproj) | `net11.0` | Production |
| [NiTorrent.Infrastructure](../../src/Modules/Torrents/Infrastructure/NiTorrent.Infrastructure.csproj) | `net11.0` | Production |
| [NiTorrent.Updates.Contracts](../../src/Modules/Updates/Contracts/NiTorrent.Updates.Contracts.csproj) | `net11.0` | Production |
| [NiTorrent.Updates.Windows](../../src/Modules/Updates/Windows/NiTorrent.Updates.Windows.csproj) | `net11.0-windows10.0.26100.0` | Production |
| [NiTorrent.App](../../src/NiTorrent.App/NiTorrent.App.csproj) | `net11.0-windows10.0.26100.0` | Production |
| [NiTorrent.Application.Tests](../../tests/NiTorrent.Application.Tests/NiTorrent.Application.Tests.csproj) | `net11.0` | Исторический тестовый проект, не активирован |
| [NiTorrent.Architecture.Tests](../../tests/NiTorrent.Architecture.Tests/NiTorrent.Architecture.Tests.csproj) | `net11.0-windows10.0.26100.0` | Активный тестовый проект |
| [NiTorrent.Domain.Tests](../../tests/NiTorrent.Domain.Tests/NiTorrent.Domain.Tests.csproj) | `net11.0` | Исторический тестовый проект, не активирован |
| [NiTorrent.Infrastructure.Tests](../../tests/NiTorrent.Infrastructure.Tests/NiTorrent.Infrastructure.Tests.csproj) | `net11.0` | Исторический тестовый проект, не активирован |
| [NiTorrent.Lifecycle.Tests](../../tests/NiTorrent.Lifecycle.Tests/NiTorrent.Lifecycle.Tests.csproj) | `net11.0` | Активный тестовый проект |
| [StartupBaseline](../../tools/startup-baseline/StartupBaseline.csproj) | `$(BaselineFramework)` | Изолированный инструмент |

У StartupBaseline TFM параметризован через BaselineFramework; значение по умолчанию — net11.0. Отображённое выражение MSBuild не является отдельным названием платформы.

| Пакет | Версия в csproj | Прямые потребители |
| --- | --- | --- |
| `CommunityToolkit.Common` | `8.4.2` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `CommunityToolkit.Mvvm` | `8.4.2` | NiTorrent.App, NiTorrent.Desktop.WinUI, NiTorrent.Presentation |
| `coverlet.collector` | `10.1.1` | NiTorrent.Application.Tests, NiTorrent.Domain.Tests, NiTorrent.Infrastructure.Tests |
| `DevWinUI` | `11.1.0` | NiTorrent.App, NiTorrent.Desktop.WinUI, NiTorrent.Updates.Windows |
| `DevWinUI.ContextMenu` | `11.1.0` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `DevWinUI.SourceGenerator` | `10.9.0` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `H.NotifyIcon.WinUI` | `2.4.1` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `Microsoft.Extensions.Configuration` | `11.0.0` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `Microsoft.Extensions.DependencyInjection` | `11.0.0` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `Microsoft.Extensions.Hosting` | `11.0.0` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `Microsoft.Extensions.Hosting.WindowsServices` | `11.0.0` | NiTorrent.Infrastructure |
| `Microsoft.Extensions.Logging` | `11.0.0` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `Microsoft.NET.Test.Sdk` | `18.12.0` | NiTorrent.Application.Tests, NiTorrent.Architecture.Tests, NiTorrent.Domain.Tests, NiTorrent.Infrastructure.Tests, NiTorrent.Lifecycle.Tests |
| `Microsoft.Windows.CsWinRT` | `2.3.1` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `Microsoft.Windows.SDK.BuildTools` | `10.0.28000.2957` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `Microsoft.WindowsAppSDK` | `2.7.0` | NiTorrent.App, NiTorrent.Desktop.WinUI, NiTorrent.Updates.Windows |
| `Microsoft.Xaml.Behaviors.WinUI.Managed` | `3.0.1` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `MonoTorrent` | `3.0.2` | NiTorrent.Torrents.MonoTorrent |
| `Newtonsoft.Json` | `13.0.4` | NiTorrent.App, NiTorrent.Desktop.WinUI, NiTorrent.Infrastructure |
| `nucs.JsonSettings` | `2.3.2` | NiTorrent.App, NiTorrent.Desktop.WinUI, NiTorrent.Infrastructure |
| `nucs.JsonSettings.AutoSaveGenerator` | `2.0.4` | NiTorrent.App, NiTorrent.Desktop.WinUI, NiTorrent.Infrastructure |
| `WinUIEx` | `2.9.3` | NiTorrent.App, NiTorrent.Desktop.WinUI |
| `xunit` | `2.9.3` | NiTorrent.Application.Tests, NiTorrent.Architecture.Tests, NiTorrent.Domain.Tests, NiTorrent.Infrastructure.Tests, NiTorrent.Lifecycle.Tests |
| `xunit.runner.visualstudio` | `4.0.1` | NiTorrent.Application.Tests, NiTorrent.Architecture.Tests, NiTorrent.Domain.Tests, NiTorrent.Infrastructure.Tests, NiTorrent.Lifecycle.Tests |

Наличие прямого пакета в UI-проекте само по себе не определяет владельца логики. MonoTorrent должен оставаться в сборке адаптера; Architecture.Tests проверяет пакет, ссылки типов и разрешённые зависимости сборок. В текущей реализации проверок используется стандартный System.Reflection.Metadata, а не дополнительная библиотека NetArchTest/ArchUnitNET.
