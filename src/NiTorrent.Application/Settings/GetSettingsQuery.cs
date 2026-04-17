namespace NiTorrent.Application.Settings;

public sealed class GetSettingsQuery(ISettingsRepository settingsRepositorys)
{
    private readonly ISettingsRepository repository = settingsRepositorys;
    public Task<AppSettings> ExecuteAsync(CancellationToken ct = default)
        => repository.GetAsync(ct);
}
