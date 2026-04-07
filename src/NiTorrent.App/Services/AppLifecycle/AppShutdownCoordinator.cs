using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class AppShutdownCoordinator : IAppShutdownCoordinator
{
    private static readonly TimeSpan ShutdownStepTimeout = TimeSpan.FromSeconds(3);

    private readonly ITorrentEngineMaintenanceService _engineMaintenanceService;
    private readonly ITorrentEngineLifecycle _engineLifecycle;
    private readonly IMainWindowLifecycle _mainWindowLifecycle;
    private readonly ILogger<AppShutdownCoordinator> _logger;
    private readonly IUiDispatcher _dispatcher;

    public AppShutdownCoordinator(
        ITorrentEngineMaintenanceService engineMaintenanceService,
        ITorrentEngineLifecycle engineLifecycle,
        IMainWindowLifecycle mainWindowLifecycle,
        IUiDispatcher dispatcher,
        ILogger<AppShutdownCoordinator> logger)
    {
        _engineMaintenanceService = engineMaintenanceService;
        _engineLifecycle = engineLifecycle;
        _mainWindowLifecycle = mainWindowLifecycle;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task ShutdownAsync(Func<Task> stopHostAsync, Action exitApplication)
    {
        await RunShutdownStepAsync(
                "torrent service shutdown",
                ct => _engineMaintenanceService.ShutdownAsync(ct),
                ShutdownStepTimeout)
            .ConfigureAwait(false);

        await RunShutdownStepAsync(
                "torrent engine lifecycle shutdown",
                ct => _engineLifecycle.ShutdownAsync(ct),
                ShutdownStepTimeout)
            .ConfigureAwait(false);

        await RunShutdownStepAsync(
                "main window close for shutdown",
                _ => _mainWindowLifecycle.CloseForShutdownAsync(),
                ShutdownStepTimeout)
            .ConfigureAwait(false);

        await RunShutdownStepAsync(
                "host stop",
                _ => stopHostAsync(),
                ShutdownStepTimeout)
            .ConfigureAwait(false);

        try
        {
            _mainWindowLifecycle.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Window lifecycle dispose failed");
        }
        finally
        {
            try
            {
                await _dispatcher.EnqueueAsync(exitApplication).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch app exit on UI thread; forcing process termination");
            }

            // Hard-stop fallback: even if window is gone, process must not stay in background.
            Environment.Exit(0);
        }
    }

    private async Task RunShutdownStepAsync(
        string stepName,
        Func<CancellationToken, Task> step,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        Task stepTask;

        try
        {
            stepTask = step(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shutdown step '{StepName}' failed before start", stepName);
            return;
        }

        var completed = await Task.WhenAny(stepTask, Task.Delay(timeout)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, stepTask))
        {
            _logger.LogWarning("Shutdown step '{StepName}' timed out after {TimeoutMs} ms; continuing exit", stepName, timeout.TotalMilliseconds);
            return;
        }

        try
        {
            await stepTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning("Shutdown step '{StepName}' was canceled by timeout after {TimeoutMs} ms; continuing exit", stepName, timeout.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shutdown step '{StepName}' failed; continuing exit", stepName);
        }
    }
}
