using NiTorrent.Application.Settings;
using Nucs.JsonSettings;
using Nucs.JsonSettings.Fluent;
using Nucs.JsonSettings.Modulation.Recovery;

namespace NiTorrent.Infrastructure.Settings;

public class SettingsRepository(AppJsonSettings appJsonSettings) : ISettingsRepository
{
    private AppJsonSettings _jsonSettings = appJsonSettings;

    private Task update = null;
    public Task<AppSettings> GetAsync(CancellationToken ct)
    {
        EnsureLoaded();
        return Task.FromResult(Map(_jsonSettings));
    }

    public async Task SaveAsync(AppSettings newSettings, CancellationToken ct)
    {
        _jsonSettings.EngineSettings = newSettings.EngineSettings;
        if (update is not null)
        {
            await update;
        }
        update = Task.Run(_jsonSettings.Save);
    }

    private void EnsureLoaded()
    {
        if (_jsonSettings is null)
        {
            _jsonSettings = JsonSettings.Configure<AppJsonSettings>()
                .WithRecovery(RecoveryAction.RenameAndLoadDefault)
                .LoadNow();
        }
    }

    private AppSettings Map(AppJsonSettings jsonSettings)
    {
        return new AppSettings
        {
            EngineSettings = jsonSettings.EngineSettings
        };
    }
}
