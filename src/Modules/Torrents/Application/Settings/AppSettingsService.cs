namespace NiTorrent.Application.Settings;

public sealed class AppSettingsService(ISettingsRepository repository)
{
    private readonly ISettingsRepository _repository = repository;

    public AppSettings Current { get; private set; } = new AppSettings(); //AppSettings.Default;

    public event Action<AppSettings>? Changed;

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
