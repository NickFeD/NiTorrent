using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.Presentation.Features.Settings;

public partial class ThemeSettingsViewModel : ObservableObject
{
    private static readonly HashSet<string> AllowedThemes =
    [
        "Light",
        "Dark",
        "Default"
    ];

    private static readonly HashSet<string> AllowedBackdrops =
    [
        "Mica",
        "MicaAlt",
        "Acrylic",
        "AcrylicThin"
    ];

    private readonly IThemeSettingsService _themeSettingsService;

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "Default";

    [ObservableProperty]
    public partial string SelectedBackdrop { get; set; } = "Mica";

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    private bool _isLoading;
    private bool _initialized;

    public ThemeSettingsViewModel(IThemeSettingsService themeSettingsService)
    {
        _themeSettingsService = themeSettingsService;
    }

    partial void OnSelectedThemeChanged(string value) => MarkDirty();

    partial void OnSelectedBackdropChanged(string value) => MarkDirty();

    private void MarkDirty()
    {
        if (!_isLoading)
            HasUnsavedChanges = true;
    }

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return;

        await ReloadAsync(ct).ConfigureAwait(false);
        _initialized = true;
    }

    [RelayCommand]
    private Task ReloadAsync()
        => ReloadAsync(CancellationToken.None);

    private async Task ReloadAsync(CancellationToken ct)
    {
        var settings = await _themeSettingsService.LoadAsync(ct).ConfigureAwait(false);

        _isLoading = true;
        try
        {
            SelectedTheme = Normalize(settings.ElementTheme, AllowedThemes, "Default");
            SelectedBackdrop = Normalize(settings.BackdropType, AllowedBackdrops, "Mica");
            HasUnsavedChanges = false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var draft = new ThemeSettingsDraft(
            Normalize(SelectedTheme, AllowedThemes, "Default"),
            Normalize(SelectedBackdrop, AllowedBackdrops, "Mica"));

        await _themeSettingsService.SaveAndApplyAsync(draft).ConfigureAwait(false);
        HasUnsavedChanges = false;
    }

    private static string Normalize(string? value, HashSet<string> allowed, string fallback)
        => string.IsNullOrWhiteSpace(value) || !allowed.Contains(value)
            ? fallback
            : value;
}
