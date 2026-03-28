using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed class HandleWindowCloseWorkflow(ITorrentSettingsRepository settingsRepository)
{
    public AppShellCloseAction Execute()
        => AppShellClosePolicy.Resolve(
            settingsRepository.LoadAsync().GetAwaiter().GetResult().CloseBehavior,
            AppShellCloseRequestSource.MainWindow);
}
