using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Abstractions;

public interface IAppShellSettingsService
{
    AppCloseBehavior GetCloseBehavior();
    Task SaveCloseBehaviorAsync(AppCloseBehavior behavior, CancellationToken ct = default);
}
