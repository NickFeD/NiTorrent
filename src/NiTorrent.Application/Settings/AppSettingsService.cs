namespace NiTorrent.Application.Settings;

public sealed class AppSettingsService(ISettingsRepository repository) : IAppStartupTask
{
    private readonly ISettingsRepository _repository = repository;

    public AppSettings Current { get; private set; } = new AppSettings(); //AppSettings.Default;

    public StartupStage Stage => StartupStage.Critical;

    public int Order => 999;

    public bool CanRunInParallel => false;

    public event Action<AppSettings>? Changed;

    public Task ExecuteAsync(CancellationToken ct)
    {
        return InitializeAsync(ct);
    }

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
