namespace NiTorrent.Application.Settings;

public sealed class AppSettingsService(ISettingsRepository repository) : IAppLifecycleTask
{
    private readonly ISettingsRepository _repository = repository;

    public AppSettings Current { get; private set; } = new AppSettings(); //AppSettings.Default;

    public string Name => "Load app settings";

    public AppStartupStage Stage => AppStartupStage.Bootstrap;

    public int Order => 999;

    public event Action<AppSettings>? Changed;

    public Task StartAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => InitializeAsync(cancellationToken);

    public Task StopAsync(AppLifecycleContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Current = await _repository.GetAsync(ct);
        Changed?.Invoke(Current);
    }

    public async Task UpdateAsync(AppSettings newSettings, CancellationToken ct = default)
    {
        await _repository.SaveAsync(newSettings, ct);
        Current = newSettings;
        Changed?.Invoke(Current);
    }
}
