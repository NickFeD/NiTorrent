using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed class HandleWindowCloseWorkflow(ITorrentSettingsRepository settingsRepository)
{
    public async Task<AppShellCloseAction> ExecuteAsync(CancellationToken ct = default)
    {
        var settings = await settingsRepository.LoadAsync(ct).ConfigureAwait(false);
        return AppShellClosePolicy.Resolve(settings.CloseBehavior, AppShellCloseRequestSource.MainWindow);
    }
}
