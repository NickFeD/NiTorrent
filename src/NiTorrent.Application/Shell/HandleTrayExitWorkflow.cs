using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed class HandleTrayExitWorkflow(ITorrentSettingsRepository settingsRepository)
{
    public AppShellCloseAction Execute()
    {
        var settings = settingsRepository.LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return AppShellClosePolicy.Resolve(settings.CloseBehavior, AppShellCloseRequestSource.TrayExit);
    }
}
