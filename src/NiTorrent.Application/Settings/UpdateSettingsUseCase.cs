namespace NiTorrent.Application.Settings;

public class UpdateSettingsUseCase(AppSettingsService settingsService, IEngineSettingsService engineSettingsService)
{
    private readonly AppSettingsService _settingsService = settingsService;
    private readonly IEngineSettingsService _engineSettingsService = engineSettingsService;
    public async Task ExecuteAsync(
        SettingsCommand command,
        CancellationToken ct = default)
    {
        await _settingsService.UpdateAsync(command.NewSettings, ct);

        await _engineSettingsService.ApplySettingsAsync(command.NewSettings.EngineSettings, ct);
    }

}
