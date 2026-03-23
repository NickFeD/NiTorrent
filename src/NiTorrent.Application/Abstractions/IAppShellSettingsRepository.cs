using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Abstractions;

public interface IAppShellSettingsRepository
{
    AppShellSettings Load();
    void Save(AppShellSettings settings);
}
