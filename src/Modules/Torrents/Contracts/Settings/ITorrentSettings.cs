using NiTorrent.Application.Settings;

namespace NiTorrent.Torrents.Contracts;

public interface ITorrentSettings
{
    AppSettings Current { get; }
    Task<AppSettings> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(AppSettings settings, CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
