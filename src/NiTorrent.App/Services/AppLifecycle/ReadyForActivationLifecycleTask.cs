using Microsoft.Extensions.Logging;
using NiTorrent.Application;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class ReadyForActivationLifecycleTask(
    IActivationQueue activationQueue,
    ILogger<ReadyForActivationLifecycleTask> logger) : IAppLifecycleTask
{
    private readonly IActivationQueue _activationQueue = activationQueue;
    private readonly ILogger<ReadyForActivationLifecycleTask> _logger = logger;

    public string Name => "Ready for queued activation";

    public AppStartupStage Stage => AppStartupStage.Ready;

    public int Order => 0;

    public async Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Lifecycle] ReadyForActivation");
        await _activationQueue.DrainAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
