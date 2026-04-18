namespace NiTorrent.Application;

public sealed class AppCloseCoordinator
{
    private readonly IServiceProvider _serviceProvider;
    private IReadOnlyList<IAppShutdownTask>? _shutdownTasks;
    private int _closeStarted;

    public AppCloseCoordinator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ClosingAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _closeStarted, 1) == 1)
            return;

        var shutdownTasks = GetShutdownTasks();

        foreach (var task in shutdownTasks)
        {
            try
            {
                await task.ExecuteAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // best-effort shutdown: continue to remaining tasks and final action
            }
        }
    }

    private IReadOnlyList<IAppShutdownTask> GetShutdownTasks()
    {
        if (_shutdownTasks is not null)
            return _shutdownTasks;

        var resolved = _serviceProvider.GetService(typeof(IEnumerable<IAppShutdownTask>))
            as IEnumerable<IAppShutdownTask>;

        _shutdownTasks = (resolved ?? [])
            .OrderBy(x => x.Order)
            .ToList();

        return _shutdownTasks;
    }
}
