using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Settings;

public sealed class GetSettingsQuery(ITorrentSettingsRepository repository)
{
    public Task<TorrentSettingsDraft> ExecuteAsync(CancellationToken ct = default)
        => repository.LoadAsync(ct);
}
