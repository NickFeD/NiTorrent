using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

/// <summary>
/// Transitional application service for the future details page and per-torrent settings.
/// It keeps the product-facing contract outside of Presentation and Infrastructure.
/// </summary>
public sealed class TorrentDetailsService : ITorrentDetailsService
{
    private readonly ITorrentService _torrentService;
    private readonly ITorrentEntrySettingsRepository _settingsRepository;

    public TorrentDetailsService(ITorrentService torrentService, ITorrentEntrySettingsRepository settingsRepository)
    {
        _torrentService = torrentService;
        _settingsRepository = settingsRepository;
    }

    public TorrentDetailsReadModel? Get(TorrentId torrentId)
    {
        if (!_torrentService.TryGet(torrentId, out var snapshot) || snapshot is null)
            return null;

        return new TorrentDetailsReadModel(snapshot, _settingsRepository.Load(torrentId));
    }

    public TorrentEntrySettings GetSettings(TorrentId torrentId)
        => _settingsRepository.Load(torrentId);

    public void SaveSettings(TorrentId torrentId, TorrentEntrySettings settings)
        => _settingsRepository.Save(torrentId, settings);
}
