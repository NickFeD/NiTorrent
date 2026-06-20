using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.App.Services;
using NiTorrent.Application;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Presentation.Abstractions;
using System.Diagnostics;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class NiTorrentApplication(
    IHost host,
    IServiceProvider services,
    Action exit) : INiTorrentApplication
{
    private readonly IHost _host = host;
    private readonly IServiceProvider _services = services;
    private readonly Action _exit = exit;
    private readonly object _startupGate = new();
    private readonly Stopwatch _shutdownElapsed = new();
    private Task? _startupTask;
    private int _shutdownStarted;

    public async Task StartAsync(AppActivationArguments activationArgs, CancellationToken cancellationToken)
    {
        _services.GetService<ILogger<NiTorrentApplication>>()?
            .LogInformation("[Lifecycle] Activation received: {ActivationKind}", ActivationLogFormatter.Describe(activationArgs));
        GetService<IActivationQueue>().Enqueue(activationArgs);

        var startupTask = EnsureStartupStarted(activationArgs, cancellationToken);
        await startupTask.ConfigureAwait(false);
    }

    public async Task HandleActivationAsync(AppActivationArguments activationArgs, CancellationToken cancellationToken)
    {
        _services.GetService<ILogger<NiTorrentApplication>>()?
            .LogInformation("[Lifecycle] Activation received: {ActivationKind}", ActivationLogFormatter.Describe(activationArgs));
        var activationQueue = GetService<IActivationQueue>();
        activationQueue.Enqueue(activationArgs);

        var startupTask = Volatile.Read(ref _startupTask);
        if (startupTask is null)
            return;

        await startupTask.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        await activationQueue.DrainAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ShutdownAsync(AppShutdownReason reason, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
            return;

        var logger = _services.GetService<ILogger<NiTorrentApplication>>();
        var timeoutOptions = _services.GetService<ShutdownTimeoutOptions>() ?? new ShutdownTimeoutOptions();
        var dispatcher = GetService<IUiDispatcher>();
        var lifecycleCoordinator = GetService<AppLifecycleCoordinator>();
        var context = CreateLifecycleContext(null, cancellationToken);

        _shutdownElapsed.Restart();
        logger?.LogInformation("[Lifecycle] Shutdown requested: {Reason}", reason);

        await RunShutdownStepAsync(
            "Wait for startup completion",
            timeoutOptions.HostStopTimeout,
            async ct =>
            {
                var startupTask = Volatile.Read(ref _startupTask);
                if (startupTask is not null)
                    await startupTask.WaitAsync(ct).ConfigureAwait(false);
            },
            logger,
            cancellationToken).ConfigureAwait(false);

        await RunShutdownStepAsync(
            "Stop accepting activation",
            timeoutOptions.ShellCloseTimeout,
            ct =>
            {
                ct.ThrowIfCancellationRequested();
                GetService<IActivationQueue>().StopAccepting();
                return Task.CompletedTask;
            },
            logger,
            cancellationToken).ConfigureAwait(false);

        await RunShutdownStepAsync(
            "Stop accepting torrent work",
            timeoutOptions.EngineStopTimeout,
            ct => GetService<ITorrentEngineLifecycle>().StopAcceptingWorkAsync(ct),
            logger,
            cancellationToken).ConfigureAwait(false);

        await RunShutdownStepAsync(
            "Save UI state and settings",
            timeoutOptions.SessionFlushTimeout,
            ct => GetService<ISettingsRepository>().FlushAsync(ct),
            logger,
            cancellationToken).ConfigureAwait(false);

        await RunShutdownStepAsync(
            "Close shell, dialogs and flyouts",
            timeoutOptions.ShellCloseTimeout,
            ct => GetService<IAppShellLifecycle>().CloseAsync(),
            logger,
            cancellationToken).ConfigureAwait(false);

        await RunShutdownStepAsync(
            "Stop lifecycle hooks and runtime tasks",
            timeoutOptions.EngineStopTimeout,
            ct => lifecycleCoordinator.StopAsync(
                context,
                ct,
                task => SelectShutdownTimeout(task, timeoutOptions)),
            logger,
            cancellationToken).ConfigureAwait(false);

        await RunShutdownStepAsync(
            "Stop hosted services",
            timeoutOptions.HostStopTimeout,
            ct => _host.StopAsync(ct),
            logger,
            cancellationToken).ConfigureAwait(false);

        await RunShutdownStepAsync(
            "Write clean shutdown marker",
            timeoutOptions.SessionFlushTimeout,
            ct => GetService<IShutdownStateService>().MarkCleanShutdownAsync(ct),
            logger,
            cancellationToken).ConfigureAwait(false);

        logger?.LogInformation(
            "[Lifecycle] Clean shutdown completed in {ElapsedMs} ms",
            _shutdownElapsed.ElapsedMilliseconds);

        await RunShutdownStepAsync(
            "Dispose host",
            timeoutOptions.HostStopTimeout,
            ct => dispatcher.EnqueueAsync(_host.Dispose, ct),
            logger,
            cancellationToken).ConfigureAwait(false);

        await RunShutdownStepAsync(
            "Exit dispatcher/application",
            timeoutOptions.ShellCloseTimeout,
            ct => dispatcher.EnqueueAsync(_exit, ct),
            logger,
            cancellationToken).ConfigureAwait(false);
    }

    private Task EnsureStartupStarted(AppActivationArguments activationArgs, CancellationToken cancellationToken)
    {
        lock (_startupGate)
        {
            _startupTask ??= StartCoreAsync(activationArgs, cancellationToken);
            return _startupTask;
        }
    }

    private async Task StartCoreAsync(AppActivationArguments activationArgs, CancellationToken cancellationToken)
    {
        var holder = GetService<UiDispatcherHolder>();
        holder.Initialize(DispatcherQueue.GetForCurrentThread());

        var logger = _services.GetService<ILogger<NiTorrentApplication>>();
        logger?.LogInformation("[Lifecycle] Host start requested");
        var elapsed = Stopwatch.StartNew();
        await _host.StartAsync(cancellationToken).ConfigureAwait(false);
        elapsed.Stop();
        logger?.LogInformation("[Lifecycle] Host start completed in {ElapsedMs} ms", elapsed.ElapsedMilliseconds);

        var lifecycleCoordinator = GetService<AppLifecycleCoordinator>();
        var context = CreateLifecycleContext(activationArgs, cancellationToken);
        await lifecycleCoordinator.StartAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private AppLifecycleContext CreateLifecycleContext(AppActivationArguments? activationArgs, CancellationToken cancellationToken)
        => new(
            _services,
            activationArgs,
            cancellationToken,
            SynchronizationContext.Current,
            GetService<IUiDispatcher>(),
            _services.GetService<ILogger<NiTorrentApplication>>());

    private static TimeSpan? SelectShutdownTimeout(IAppLifecycleTask task, ShutdownTimeoutOptions options)
    {
        if (task.Name.Contains("shell", StringComparison.OrdinalIgnoreCase) ||
            task.Name.Contains("tray", StringComparison.OrdinalIgnoreCase))
        {
            return options.ShellCloseTimeout;
        }

        if (task.Name.Contains("torrent runtime state", StringComparison.OrdinalIgnoreCase) ||
            task.Name.Contains("settings", StringComparison.OrdinalIgnoreCase) ||
            task.Name.Contains("flush", StringComparison.OrdinalIgnoreCase))
        {
            return options.SessionFlushTimeout;
        }

        if (task.Name.Contains("torrent", StringComparison.OrdinalIgnoreCase) ||
            task.Name.Contains("engine", StringComparison.OrdinalIgnoreCase))
        {
            return options.EngineStopTimeout;
        }

        return options.ShellCloseTimeout;
    }

    private static async Task RunShutdownStepAsync(
        string name,
        TimeSpan timeout,
        Func<CancellationToken, Task> action,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        logger?.LogInformation("[Lifecycle] Shutdown step started: {StepName}", name);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task operation;
        try
        {
            operation = action(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            elapsed.Stop();
            logger?.LogWarning(
                "[Lifecycle] Shutdown step canceled after {ElapsedMs} ms: {StepName}",
                elapsed.ElapsedMilliseconds,
                name);
            throw;
        }
        catch (Exception ex)
        {
            elapsed.Stop();
            logger?.LogError(
                ex,
                "[Lifecycle] Shutdown step failed after {ElapsedMs} ms: {StepName}. Shutdown will continue.",
                elapsed.ElapsedMilliseconds,
                name);
            return;
        }

        var delay = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(operation, delay).ConfigureAwait(false);

        if (completed != operation)
        {
            timeoutCts.Cancel();
            elapsed.Stop();
            logger?.LogWarning(
                "[Lifecycle] Shutdown step timed out after {ElapsedMs} ms: {StepName}. Shutdown will continue.",
                elapsed.ElapsedMilliseconds,
                name);

            _ = ObserveTimedOutShutdownStepAsync(name, operation, logger);
            return;
        }

        try
        {
            await operation.ConfigureAwait(false);
            elapsed.Stop();
            logger?.LogInformation(
                "[Lifecycle] Shutdown step completed in {ElapsedMs} ms: {StepName}",
                elapsed.ElapsedMilliseconds,
                name);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            elapsed.Stop();
            logger?.LogWarning(
                "[Lifecycle] Shutdown step canceled after {ElapsedMs} ms: {StepName}",
                elapsed.ElapsedMilliseconds,
                name);
            throw;
        }
        catch (Exception ex)
        {
            elapsed.Stop();
            logger?.LogError(
                ex,
                "[Lifecycle] Shutdown step failed after {ElapsedMs} ms: {StepName}. Shutdown will continue.",
                elapsed.ElapsedMilliseconds,
                name);
        }
    }

    private static async Task ObserveTimedOutShutdownStepAsync(string name, Task operation, ILogger? logger)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Timed out shutdown step completed after shutdown continued: {StepName}", name);
        }
    }

    private T GetService<T>() where T : class
    {
        if (_services.GetService(typeof(T)) is not T service)
            throw new ArgumentException($"{typeof(T)} needs to be registered in the application service provider.");

        return service;
    }
}
