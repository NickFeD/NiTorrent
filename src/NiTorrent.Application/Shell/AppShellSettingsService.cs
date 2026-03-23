using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Shell;

public sealed class AppShellSettingsService : IAppShellSettingsService
{
    private readonly IAppShellSettingsRepository _repository;
    private AppShellSettings? _cached;

    public AppShellSettingsService(IAppShellSettingsRepository repository)
    {
        _repository = repository;
    }

    public AppShellSettings GetCurrent()
        => _cached ??= _repository.Load();

    public AppCloseBehavior GetCloseBehavior()
        => GetCurrent().CloseBehavior;

    public void Save(AppShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _repository.Save(settings);
        _cached = Clone(settings);
    }

    public void SetCloseBehavior(AppCloseBehavior behavior)
    {
        var settings = GetCurrent();
        if (settings.CloseBehavior == behavior)
            return;

        settings.CloseBehavior = behavior;
        Save(settings);
    }

    private static AppShellSettings Clone(AppShellSettings settings)
        => new()
        {
            CloseBehavior = settings.CloseBehavior,
        };
}
