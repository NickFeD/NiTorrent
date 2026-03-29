using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed class HandleTrayExitWorkflow
{
    public AppShellCloseAction Execute()
        => AppShellCloseAction.ExitApplication;
}
