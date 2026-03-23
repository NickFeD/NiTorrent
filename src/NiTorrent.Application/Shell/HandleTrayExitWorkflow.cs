using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed class HandleTrayExitWorkflow(IAppShellSettingsService shellSettings)
{
    public AppShellCloseAction Execute()
        => AppShellClosePolicy.Resolve(
            shellSettings.GetCloseBehavior(),
            AppShellCloseRequestSource.TrayExit);
}
