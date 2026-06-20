using NiTorrent.Application;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class MainWindowLifecycleTask(IAppShellLifecycle shellLifecycle) : IAppLifecycleTask, IAppLifecycleShutdownStep
{
    private readonly IAppShellLifecycle _shellLifecycle = shellLifecycle;

    public string Name => "Start main shell";

    public AppStartupStage Stage => AppStartupStage.Shell;

    public int Order => 0;

    public int ShutdownOrder => 300;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _shellLifecycle.StartAsync(cancellationToken);

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _shellLifecycle.CloseAsync();
}
