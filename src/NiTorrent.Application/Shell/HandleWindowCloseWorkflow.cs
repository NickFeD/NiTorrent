using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed class HandleWindowCloseWorkflow()
{
    public async Task<AppShellCloseAction> ExecuteAsync(CancellationToken ct = default)
    {
        return AppShellClosePolicy.Resolve(AppCloseBehavior.MinimizeToTray, AppShellCloseRequestSource.MainWindow);
    }
}
