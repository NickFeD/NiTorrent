using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services;

public sealed class ThemeSettingsService(IThemeService themeService) : IThemeSettingsService
{
    private readonly IThemeService _themeService = themeService;

    public Task<ThemeSettingsDraft> LoadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var theme = _themeService.ElementTheme.ToString();
        var backdrop = _themeService.BackdropType.ToString();

        return Task.FromResult(new ThemeSettingsDraft(theme, backdrop));
    }

    public async Task SaveAndApplyAsync(ThemeSettingsDraft settings, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await _themeService.SetElementThemeAsync(Enum.Parse<ElementTheme>(settings.ElementTheme, true)).ConfigureAwait(false);
        await _themeService.SetBackdropTypeAsync(Enum.Parse<BackdropType>(settings.BackdropType, true)).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }
}
