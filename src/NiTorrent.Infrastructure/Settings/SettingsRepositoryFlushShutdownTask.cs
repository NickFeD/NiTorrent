using NiTorrent.Application;
using NiTorrent.Application.Settings;

namespace NiTorrent.Infrastructure.Settings;

internal sealed class SettingsRepositoryFlushShutdownTask(ISettingsRepository repository) : IAppLifecycleTask, IAppLifecycleShutdownStep
{
    private readonly ISettingsRepository _repository = repository;

    public string Name => "Flush settings repository";

    public AppStartupStage Stage => AppStartupStage.Bootstrap;

    public int Order => 100;

    public int ShutdownOrder => 200;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => _repository.FlushAsync(cancellationToken);
}
