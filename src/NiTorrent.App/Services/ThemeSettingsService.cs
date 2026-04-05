using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services;

public sealed class ThemeSettingsService(IThemeService themeService) : IThemeSettingsService
{
    private readonly IThemeService _themeService = themeService;

    public Task<ThemeSettingsDraft> LoadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var theme = ReadStringProperty("ElementTheme", "Default");
        var backdrop = ReadStringProperty("BackdropType", "Mica");

        return Task.FromResult(new ThemeSettingsDraft(theme, backdrop));
    }

    public async Task SaveAndApplyAsync(ThemeSettingsDraft settings, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await InvokeAsync("SetElementThemeAsync", settings.ElementTheme, ct).ConfigureAwait(false);
        await InvokeAsync("SetBackdropTypeAsync", settings.BackdropType, ct).ConfigureAwait(false);
    }

    private string ReadStringProperty(string propertyName, string fallback)
    {
        var property = _themeService.GetType().GetProperty(propertyName);
        if (property is null)
            return fallback;

        var value = property.GetValue(_themeService);
        return value?.ToString() ?? fallback;
    }

    private async Task InvokeAsync(string methodName, string value, CancellationToken ct)
    {
        var methods = _themeService.GetType()
            .GetMethods()
            .Where(x => x.Name == methodName && x.GetParameters().Length == 1)
            .ToList();

        if (methods.Count == 0)
            throw new InvalidOperationException($"{methodName} is not available on IThemeService implementation.");

        var method = methods[0];
        var parameterType = method.GetParameters()[0].ParameterType;
        object converted = value;

        if (parameterType.IsEnum)
            converted = Enum.Parse(parameterType, value, ignoreCase: true);
        else if (parameterType != typeof(string) && parameterType != typeof(object))
            converted = Convert.ChangeType(value, parameterType);

        var task = method.Invoke(_themeService, [converted]) as Task;
        if (task is not null)
            await task.ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();
    }
}
