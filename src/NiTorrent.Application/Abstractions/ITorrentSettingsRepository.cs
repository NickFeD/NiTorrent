using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Abstractions;

public interface ITorrentSettingsRepository
{
    Task<TorrentSettingsDraft> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(TorrentSettingsDraft settings, CancellationToken ct = default);
}
