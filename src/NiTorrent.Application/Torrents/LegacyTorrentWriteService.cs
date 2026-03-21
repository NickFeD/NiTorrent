using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

/// <summary>
/// Transition-only application write boundary over the legacy torrent service.
/// It keeps use cases and workflow orchestration from depending on ITorrentService directly.
/// </summary>
public sealed class LegacyTorrentWriteService(ITorrentService torrentService) : ITorrentWriteService
{
    public Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
        => torrentService.GetPreviewAsync(source, ct);

    public Task AddAsync(AddTorrentRequest request, CancellationToken ct = default)
        => torrentService.AddAsync(request, ct);

    public Task StartAsync(TorrentId id, CancellationToken ct = default)
        => torrentService.StartAsync(id, ct);

    public Task PauseAsync(TorrentId id, CancellationToken ct = default)
        => torrentService.PauseAsync(id, ct);

    public Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => torrentService.RemoveAsync(id, deleteData, ct);

    public Task ApplySettingsAsync(CancellationToken ct = default)
        => torrentService.ApplySettingsAsync();
}
