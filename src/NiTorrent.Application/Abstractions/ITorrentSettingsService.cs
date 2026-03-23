using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentSettingsService
{
    Task<TorrentSettingsDraft> LoadAsync(CancellationToken ct = default);
    Task SaveAndApplyAsync(TorrentSettingsDraft settings, CancellationToken ct = default);
}
