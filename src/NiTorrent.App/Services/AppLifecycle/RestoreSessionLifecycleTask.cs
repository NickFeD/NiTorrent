using Microsoft.Extensions.Logging;
using NiTorrent.Application;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class RestoreSessionLifecycleTask(
    ITorrentEngineLifecycle torrentEngineLifecycle,
    ILogger<RestoreSessionLifecycleTask> logger) : IAppLifecycleTask
{
    private readonly ITorrentEngineLifecycle _torrentEngineLifecycle = torrentEngineLifecycle;
    private readonly ILogger<RestoreSessionLifecycleTask> _logger = logger;

    public string Name => "Prepare session restore";

    public AppStartupStage Stage => AppStartupStage.Restore;

    public int Order => 0;

    public async Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Lifecycle] Session restore lifecycle task started");
        await _torrentEngineLifecycle.RestoreSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
