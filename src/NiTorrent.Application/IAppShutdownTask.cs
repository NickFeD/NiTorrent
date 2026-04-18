namespace NiTorrent.Application;

public interface IAppShutdownTask
{
    int Order { get; }
    Task ExecuteAsync(CancellationToken ct);
}
