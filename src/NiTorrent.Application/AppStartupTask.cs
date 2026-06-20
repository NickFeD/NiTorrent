namespace NiTorrent.Application;

public enum AppStartupStage
{
    Bootstrap = 0,
    Core = 100,
    Shell = 200,
    Restore = 300,
    Background = 400,
    Ready = 500
}

public interface IAppLifecycleTask
{
    string Name { get; }

    AppStartupStage Stage { get; }

    int Order { get; }

    Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken);

    Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken);
}

public interface IAppLifecycleShutdownStep
{
    int ShutdownOrder { get; }
}
