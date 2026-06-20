namespace NiTorrent.App.Services.AppLifecycle;

public sealed class ShutdownTimeoutOptions
{
    public TimeSpan ShellCloseTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan EngineStopTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan SessionFlushTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan HostStopTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
