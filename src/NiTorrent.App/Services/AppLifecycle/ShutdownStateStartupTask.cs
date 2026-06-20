using Microsoft.Extensions.Logging;
using NiTorrent.Application;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class ShutdownStateStartupTask(
    IShutdownStateService shutdownStateService,
    ILogger<ShutdownStateStartupTask> logger) : IAppLifecycleTask
{
    private readonly IShutdownStateService _shutdownStateService = shutdownStateService;
    private readonly ILogger<ShutdownStateStartupTask> _logger = logger;

    public string Name => "Read previous shutdown state";

    public AppStartupStage Stage => AppStartupStage.Bootstrap;

    public int Order => 0;

    public async Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
    {
        var previousState = await _shutdownStateService.ReadPreviousStateAsync(cancellationToken).ConfigureAwait(false);
        context.SetPreviousShutdownState(previousState);

        if (previousState == PreviousShutdownState.Unclean)
        {
            _logger.LogWarning("[Lifecycle] Previous application shutdown was unclean. Recovery state is enabled.");
        }
        else
        {
            _logger.LogInformation("[Lifecycle] Previous application shutdown state: {PreviousShutdownState}", previousState);
        }

        await _shutdownStateService.MarkStartedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
