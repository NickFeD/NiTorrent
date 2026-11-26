namespace NiTorrent.Application.Abstractions;

public sealed record ThemeSettingsDraft(
    string ElementTheme,
    string BackdropType);

public interface IThemeSettingsService
{
    Task<ThemeSettingsDraft> LoadAsync(CancellationToken ct = default);
    Task SaveAndApplyAsync(ThemeSettingsDraft settings, CancellationToken ct = default);
}
