using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents.LegacyAdapters;

/// <summary>
/// Transition-only application write boundary over the legacy torrent service.
/// Keeps use cases and workflow orchestration from depending on ITorrentService directly,
/// and keeps the legacy adapter inside Infrastructure where MonoTorrent integration belongs.
/// </summary>
public sealed class LegacyTorrentWriteService(ITorrentService torrentService) : ITorrentWriteService
{
    public Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
        => torrentService.GetPreviewAsync(source, ct);

    public Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default)
        => torrentService.AddAsync(request, ct);

    public Task StartAsync(TorrentId id, CancellationToken ct = default)
        => torrentService.StartAsync(id, ct);

    public Task PauseAsync(TorrentId id, CancellationToken ct = default)
        => torrentService.PauseAsync(id, ct);

    public Task StopAsync(TorrentId id, CancellationToken ct = default)
        => torrentService.StopAsync(id, ct);

    public Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => torrentService.RemoveAsync(id, deleteData, ct);

    public Task ApplySettingsAsync(CancellationToken ct = default)
        => torrentService.ApplySettingsAsync();
}
