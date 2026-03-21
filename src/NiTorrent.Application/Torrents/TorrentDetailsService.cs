using System.Linq;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

/// <summary>
/// Transitional application service for the future details page and per-torrent settings.
/// It keeps the product-facing contract outside of Presentation and Infrastructure.
/// </summary>
public sealed class TorrentDetailsService : ITorrentDetailsService
{
    private readonly ITorrentReadModelFeed _readModelFeed;
    private readonly ITorrentEntrySettingsRepository _settingsRepository;
    private readonly ITorrentEntrySettingsRuntimeApplier _runtimeApplier;

    public TorrentDetailsService(ITorrentReadModelFeed readModelFeed, ITorrentEntrySettingsRepository settingsRepository, ITorrentEntrySettingsRuntimeApplier runtimeApplier)
    {
        _readModelFeed = readModelFeed;
        _settingsRepository = settingsRepository;
        _runtimeApplier = runtimeApplier;
    }

    public TorrentDetailsReadModel? Get(TorrentId torrentId)
    {
        var snapshot = _readModelFeed.Current.FirstOrDefault(x => x.Id == torrentId);
        if (snapshot is null)
            return null;

        return new TorrentDetailsReadModel(snapshot, _settingsRepository.Load(torrentId));
    }

    public TorrentEntrySettings GetSettings(TorrentId torrentId)
        => _settingsRepository.Load(torrentId);

    public async Task SaveSettingsAsync(TorrentId torrentId, TorrentEntrySettings settings, CancellationToken ct = default)
    {
        _settingsRepository.Save(torrentId, settings);
        await _runtimeApplier.ApplyAsync(torrentId, settings, ct).ConfigureAwait(false);
    }
}
