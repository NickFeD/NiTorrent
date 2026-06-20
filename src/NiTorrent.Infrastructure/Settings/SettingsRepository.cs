using NiTorrent.Application.Settings;
using Nucs.JsonSettings;
using Nucs.JsonSettings.Fluent;
using Nucs.JsonSettings.Modulation.Recovery;

namespace NiTorrent.Infrastructure.Settings;

public class SettingsRepository(AppJsonSettings appJsonSettings) : ISettingsRepository
{
    private AppJsonSettings _jsonSettings = appJsonSettings;

    private Task? _update;
    public Task<AppSettings> GetAsync(CancellationToken ct)
    {
        EnsureLoaded();
        return Task.FromResult(Map(_jsonSettings));
    }

    public async Task SaveAsync(AppSettings newSettings, CancellationToken ct)
    {
        _jsonSettings.EngineSettings = newSettings.EngineSettings;
        _jsonSettings.CloseBehavior = newSettings.CloseBehavior;
        if (_update is not null)
        {
            await _update;
        }
        _update = Task.Run(_jsonSettings.Save);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        var pending = _update;
        if (pending is null)
            return;

        await pending.WaitAsync(ct);
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
            EngineSettings = jsonSettings.EngineSettings,
            CloseBehavior = jsonSettings.CloseBehavior
        };
    }
}
