using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace NiTorrent.Application;

public sealed class AppLifecycleCoordinator(
    IEnumerable<IAppLifecycleTask> lifecycleTasks,
    ILogger<AppLifecycleCoordinator> logger)
{
    private readonly IReadOnlyList<IAppLifecycleTask> _tasks = lifecycleTasks
        .OrderBy(x => x.Stage)
        .ThenBy(x => x.Order)
        .ToList();
    private readonly List<IAppLifecycleTask> _startedTasks = [];
    private readonly ILogger<AppLifecycleCoordinator> _logger = logger;
    private int _startupStarted;
    private int _shutdownStarted;

    public async Task StartAsync(AppLifecycleContext context, CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _startupStarted, 1) == 1)
            return;

        foreach (var stageGroup in _tasks.GroupBy(x => x.Stage).OrderBy(x => x.Key))
        {
            var stageElapsed = Stopwatch.StartNew();
            _logger.LogInformation("[Lifecycle] Stage {Stage} started", stageGroup.Key);

            foreach (var task in stageGroup)
            {
                await StartTaskAsync(task, context, ct).ConfigureAwait(false);
            }

            stageElapsed.Stop();
            _logger.LogInformation(
                "[Lifecycle] Stage {Stage} completed in {ElapsedMs} ms",
                stageGroup.Key,
                stageElapsed.ElapsedMilliseconds);
        }

        context.MarkStarted();
        _logger.LogInformation("[Lifecycle] Startup completed");
    }

    public async Task StopAsync(
        AppLifecycleContext context,
        CancellationToken ct,
        Func<IAppLifecycleTask, TimeSpan?>? timeoutSelector = null)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
            return;

        foreach (var task in GetShutdownTasks())
        {
            await StopTaskAsync(task, context, ct, timeoutSelector?.Invoke(task)).ConfigureAwait(false);
        }

        context.MarkStopped();
    }

    private async Task StartTaskAsync(IAppLifecycleTask task, AppLifecycleContext context, CancellationToken ct)
    {
        context.MarkTaskStarting(task);
        var elapsed = Stopwatch.StartNew();
        _logger.LogInformation(
            "[Lifecycle] Task {TaskName} started at stage {Stage} with order {Order}",
            task.Name,
            task.Stage,
            task.Order);

        try
        {
            await task.StartAsync(context, ct).ConfigureAwait(false);
            _startedTasks.Add(task);
            context.MarkTaskStarted(task);
            elapsed.Stop();
            _logger.LogInformation(
                "[Lifecycle] Task {TaskName} completed in {ElapsedMs} ms at stage {Stage}",
                task.Name,
                elapsed.ElapsedMilliseconds,
                task.Stage);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            elapsed.Stop();
            context.MarkFailed();
            _logger.LogWarning(
                "[Lifecycle] Task {TaskName} canceled during startup at stage {Stage} after {ElapsedMs} ms",
                task.Name,
                task.Stage,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            elapsed.Stop();
            context.MarkFailed();
            _logger.LogError(
                ex,
                "[Lifecycle] Task {TaskName} failed during startup at stage {Stage} after {ElapsedMs} ms. Application startup cannot continue.",
                task.Name,
                task.Stage,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task StopTaskAsync(
        IAppLifecycleTask task,
        AppLifecycleContext context,
        CancellationToken ct,
        TimeSpan? timeout)
    {
        context.MarkStopping(task);
        _logger.LogInformation(
            "[Lifecycle] Shutdown task {TaskName} started at stage {Stage} with order {Order}",
            task.Name,
            task.Stage,
            task.Order);

        var elapsed = Stopwatch.StartNew();

        try
        {
            await ExecuteWithOptionalTimeoutAsync(
                task.Name,
                timeout,
                token => task.StopAsync(context, token),
                ct).ConfigureAwait(false);

            elapsed.Stop();
            _logger.LogInformation(
                "[Lifecycle] Shutdown task {TaskName} completed in {ElapsedMs} ms at stage {Stage}",
                task.Name,
                elapsed.ElapsedMilliseconds,
                task.Stage);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            elapsed.Stop();
            _logger.LogWarning(
                "[Lifecycle] Shutdown task {TaskName} canceled at stage {Stage} after {ElapsedMs} ms",
                task.Name,
                task.Stage,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            elapsed.Stop();
            _logger.LogError(
                ex,
                "[Lifecycle] Shutdown task {TaskName} failed at stage {Stage} after {ElapsedMs} ms. Shutdown will continue.",
                task.Name,
                task.Stage,
                elapsed.ElapsedMilliseconds);
        }
    }

    private IEnumerable<IAppLifecycleTask> GetShutdownTasks()
    {
        var indexed = _startedTasks
            .Select((task, index) => new { Task = task, Index = index });

        return indexed
            .OrderBy(x => x.Task is IAppLifecycleShutdownStep step ? step.ShutdownOrder : 10_000)
            .ThenByDescending(x => x.Index)
            .Select(x => x.Task);
    }

    private async Task ExecuteWithOptionalTimeoutAsync(
        string taskName,
        TimeSpan? timeout,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        if (timeout is null)
        {
            await action(cancellationToken).ConfigureAwait(false);
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task operation;
        try
        {
            operation = action(timeoutCts.Token);
        }
        catch
        {
            throw;
        }

        var delay = Task.Delay(timeout.Value, cancellationToken);
        var completed = await Task.WhenAny(operation, delay).ConfigureAwait(false);

        if (completed == operation)
        {
            await operation.ConfigureAwait(false);
            return;
        }

        timeoutCts.Cancel();
        _logger.LogWarning(
            "[Lifecycle] Shutdown task {TaskName} exceeded timeout {TimeoutMs} ms; shutdown will continue.",
            taskName,
            timeout.Value.TotalMilliseconds);

        _ = ObserveTimedOutOperationAsync(taskName, operation);
    }

    private async Task ObserveTimedOutOperationAsync(string taskName, Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Timed out lifecycle task {TaskName} completed after shutdown continued", taskName);
        }
    }
}
