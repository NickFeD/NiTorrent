using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.Application;

public enum AppLifecycleState
{
    NotStarted,
    Starting,
    Started,
    Stopping,
    Stopped,
    Failed
}

public sealed class AppLifecycleContext
{
    private readonly List<string> _startedTasks = [];

    public AppLifecycleContext(
        IServiceProvider services,
        object? activationArgs,
        CancellationToken cancellationToken,
        SynchronizationContext? uiSynchronizationContext = null,
        object? uiDispatcher = null,
        ILogger? logger = null)
    {
        Services = services;
        ActivationArgs = activationArgs;
        CancellationToken = cancellationToken;
        UiSynchronizationContext = uiSynchronizationContext;
        UiDispatcher = uiDispatcher;
        Logger = logger;
    }

    public IServiceProvider Services { get; }

    public object? ActivationArgs { get; }

    public CancellationToken CancellationToken { get; }

    public SynchronizationContext? UiSynchronizationContext { get; }

    public object? UiDispatcher { get; }

    public ILogger? Logger { get; }

    public AppLifecycleState State { get; private set; } = AppLifecycleState.NotStarted;

    public PreviousShutdownState PreviousShutdownState { get; private set; } = PreviousShutdownState.Unknown;

    public AppStartupStage? CurrentStage { get; private set; }

    public string? CurrentTaskName { get; private set; }

    public IReadOnlyList<string> StartedTasks => _startedTasks;

    internal void MarkTaskStarting(IAppLifecycleTask task)
    {
        State = AppLifecycleState.Starting;
        CurrentStage = task.Stage;
        CurrentTaskName = task.Name;
    }

    internal void MarkTaskStarted(IAppLifecycleTask task)
        => _startedTasks.Add(task.Name);

    public void SetPreviousShutdownState(PreviousShutdownState previousShutdownState)
        => PreviousShutdownState = previousShutdownState;

    internal void MarkStarted()
    {
        State = AppLifecycleState.Started;
        CurrentStage = null;
        CurrentTaskName = null;
    }

    internal void MarkStopping(IAppLifecycleTask task)
    {
        State = AppLifecycleState.Stopping;
        CurrentStage = task.Stage;
        CurrentTaskName = task.Name;
    }

    internal void MarkStopped()
    {
        State = AppLifecycleState.Stopped;
        CurrentStage = null;
        CurrentTaskName = null;
    }

    internal void MarkFailed()
        => State = AppLifecycleState.Failed;
}
