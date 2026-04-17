namespace NiTorrent.Application;

public sealed class AppStartupCoordinator(IEnumerable<IAppStartupTask> tasks)
{
    private readonly IReadOnlyList<IAppStartupTask> _tasks = tasks.ToList();

    public async Task StartCriticalAsync(CancellationToken ct)
    {
        var criticalTasks = _tasks
            .Where(x => x.Stage == StartupStage.Critical)
            .OrderBy(x => x.Order);

        foreach (var task in criticalTasks)
            await task.ExecuteAsync(ct);
    }

    public async Task StartBackgroundAsync(CancellationToken ct)
    {
        var backgroundGroups = _tasks
            .Where(x => x.Stage == StartupStage.Background)
            .GroupBy(x => x.Order)
            .OrderBy(g => g.Key);

        foreach (var group in backgroundGroups)
        {
            var sequential = group.Where(x => !x.CanRunInParallel).ToList();
            var parallel = group.Where(x => x.CanRunInParallel).ToList();

            foreach (var task in sequential)
                await task.ExecuteAsync(ct);

            if (parallel.Count > 0)
                await Task.WhenAll(parallel.Select(x => x.ExecuteAsync(ct)));
        }
    }
}

public enum StartupStage
{
    Critical,
    Background
}

public interface IAppStartupTask
{
    StartupStage Stage { get; }
    int Order { get; }
    bool CanRunInParallel { get; }

    Task ExecuteAsync(CancellationToken ct);
}
