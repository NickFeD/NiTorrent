using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents.LegacyAdapters;

public sealed class LegacyTorrentReadModelFeed : ITorrentReadModelFeed
{
    private readonly ITorrentService _torrentService;

    public LegacyTorrentReadModelFeed(ITorrentService torrentService)
    {
        _torrentService = torrentService;
        _torrentService.UpdateTorrent += OnUpdateTorrent;
    }

    public event Action<IReadOnlyList<TorrentSnapshot>>? Updated;

    public IReadOnlyList<TorrentSnapshot> GetAll() => _torrentService.GetAll();

    private void OnUpdateTorrent(IReadOnlyList<TorrentSnapshot> snapshots)
        => Updated?.Invoke(snapshots);
}
