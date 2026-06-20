using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiTorrent.Application;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppHostLifecycleService(
    AppLifecycleCoordinator lifecycleCoordinator,
    IServiceProvider services,
    ILogger<AppHostLifecycleService> logger) : IHostedLifecycleService
{
    private readonly AppLifecycleCoordinator _lifecycleCoordinator = lifecycleCoordinator;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<AppHostLifecycleService> _logger = logger;

    public Task StartingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping application lifecycle tasks from host shutdown");

        var context = new AppLifecycleContext(
            _services,
            activationArgs: null,
            cancellationToken,
            SynchronizationContext.Current,
            uiDispatcher: null,
            _logger);

        await _lifecycleCoordinator.StopAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
