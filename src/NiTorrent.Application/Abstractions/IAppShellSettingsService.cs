using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Abstractions;

public interface IAppShellSettingsService
{
    AppShellSettings GetCurrent();
    AppCloseBehavior GetCloseBehavior();
    void Save(AppShellSettings settings);
    void SetCloseBehavior(AppCloseBehavior behavior);
}
