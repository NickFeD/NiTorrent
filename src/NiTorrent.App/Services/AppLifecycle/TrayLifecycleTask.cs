using NiTorrent.Application;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class TrayLifecycleTask(MainWindowLifecycle shellLifecycle) : IAppLifecycleTask, IAppLifecycleShutdownStep
{
    private readonly MainWindowLifecycle _shellLifecycle = shellLifecycle;

    public string Name => "Initialize tray integration";

    public AppStartupStage Stage => AppStartupStage.Background;

    public int Order => 100;

    public int ShutdownOrder => 400;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _shellLifecycle.StartTrayAsync(cancellationToken);

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _shellLifecycle.StopTrayAsync(cancellationToken);
}
