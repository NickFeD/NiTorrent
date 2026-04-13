namespace NiTorrent.Application.Settings;

public class UpdateSettingsUseCase(ISettingsRepository settingsRepository, IEngineSettingsService engineSettingsService)
{
    private readonly ISettingsRepository _settingsRepository = settingsRepository;
    private readonly IEngineSettingsService _engineSettingsService = engineSettingsService;
    public async Task ExecuteAsync(
        SettingsCommand command,
        CancellationToken ct = default)
    {
        await _settingsRepository.UpdateAsync(command.NewSettings, ct);

        await _engineSettingsService.ApplySettingsAsync(command.NewSettings.EngineSettings, ct);
    }

}
