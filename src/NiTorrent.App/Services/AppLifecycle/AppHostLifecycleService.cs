using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiTorrent.Application;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppHostLifecycleService(
    AppLifecycleCoordinator lifecycleCoordinator,
    IHostApplicationLifetime applicationLifetime,
    ILogger<AppHostLifecycleService> logger) : IHostedLifecycleService
{
    private readonly AppLifecycleCoordinator _lifecycleCoordinator = lifecycleCoordinator;
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
    private readonly ILogger<AppHostLifecycleService> _logger = logger;
    private Task? _backgroundStartup;

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting critical application services");
        await _lifecycleCoordinator.StartCriticalAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        _backgroundStartup = Task.Run(
            () => RunBackgroundStartupAsync(_applicationLifetime.ApplicationStopping),
            CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping application services");
        await WaitForBackgroundStartupAsync(cancellationToken).ConfigureAwait(false);
        await _lifecycleCoordinator.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private async Task RunBackgroundStartupAsync(CancellationToken ct)
    {
        try
        {
            await _lifecycleCoordinator.StartBackgroundAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background application startup failed");
        }
    }

    private async Task WaitForBackgroundStartupAsync(CancellationToken ct)
    {
        var backgroundStartup = _backgroundStartup;
        if (backgroundStartup is null)
            return;

        await backgroundStartup.WaitAsync(ct).ConfigureAwait(false);
    }
}
