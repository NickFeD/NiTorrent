namespace NiTorrent.Application;

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
