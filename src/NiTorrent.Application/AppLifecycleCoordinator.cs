using Microsoft.Extensions.Logging;

namespace NiTorrent.Application;

public sealed class AppLifecycleCoordinator(
    IEnumerable<IAppStartupTask> startupTasks,
    IEnumerable<IAppShutdownTask> shutdownTasks,
    ILogger<AppLifecycleCoordinator> logger)
{
    private readonly IReadOnlyList<IAppStartupTask> _startupTasks = startupTasks.ToList();
    private readonly IReadOnlyList<IAppShutdownTask> _shutdownTasks = shutdownTasks.ToList();
    private readonly ILogger<AppLifecycleCoordinator> _logger = logger;
    private int _shutdownStarted;

    public async Task StartCriticalAsync(CancellationToken ct)
    {
        var criticalTasks = _startupTasks
            .Where(x => x.Stage == StartupStage.Critical)
            .OrderBy(x => x.Order);

        foreach (var task in criticalTasks)
            await RunStartupTaskAsync(task, ct).ConfigureAwait(false);
    }

    public async Task StartBackgroundAsync(CancellationToken ct)
    {
        var backgroundGroups = _startupTasks
            .Where(x => x.Stage == StartupStage.Background)
            .GroupBy(x => x.Order)
            .OrderBy(g => g.Key);

        foreach (var group in backgroundGroups)
        {
            var sequential = group.Where(x => !x.CanRunInParallel).ToList();
            var parallel = group.Where(x => x.CanRunInParallel).ToList();

            foreach (var task in sequential)
                await RunBackgroundStartupTaskAsync(task, ct).ConfigureAwait(false);

            if (parallel.Count > 0)
                await Task.WhenAll(parallel.Select(x => RunBackgroundStartupTaskAsync(x, ct))).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
            return;

        foreach (var task in _shutdownTasks.OrderByDescending(x => x.Order))
        {
            try
            {
                await task.ExecuteAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application shutdown task {TaskType} failed", task.GetType().Name);
            }
        }
    }

    private async Task RunStartupTaskAsync(IAppStartupTask task, CancellationToken ct)
    {
        try
        {
            await task.ExecuteAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application startup task {TaskType} failed", task.GetType().Name);
            throw;
        }
    }

    private async Task RunBackgroundStartupTaskAsync(IAppStartupTask task, CancellationToken ct)
    {
        try
        {
            await task.ExecuteAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background application startup task {TaskType} failed", task.GetType().Name);
        }
    }
}
