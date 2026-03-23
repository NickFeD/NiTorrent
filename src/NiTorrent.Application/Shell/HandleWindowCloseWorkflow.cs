using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed class HandleWindowCloseWorkflow(IAppShellSettingsService shellSettings)
{
    public AppShellCloseAction Execute()
        => AppShellClosePolicy.Resolve(
            shellSettings.GetCloseBehavior(),
            AppShellCloseRequestSource.MainWindow);
}
