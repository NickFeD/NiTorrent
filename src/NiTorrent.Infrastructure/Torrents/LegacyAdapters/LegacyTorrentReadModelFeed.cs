using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents.LegacyAdapters;

/// <summary>
/// Transition-only feed that adapts legacy ITorrentService updates to the application read model boundary.
/// Keeps legacy event wiring inside Infrastructure instead of Application.
/// </summary>
public sealed class LegacyTorrentReadModelFeed : ITorrentReadModelFeed, IDisposable
{
    private readonly ITorrentService _torrentService;
    private event Action<IReadOnlyList<TorrentSnapshot>>? _updated;
    private IReadOnlyList<TorrentSnapshot> _current;

    public LegacyTorrentReadModelFeed(ITorrentService torrentService)
    {
        _torrentService = torrentService;
        _current = _torrentService.GetAll();
        _torrentService.UpdateTorrent += OnUpdated;
    }

    public event Action<IReadOnlyList<TorrentSnapshot>>? Updated
    {
        add
        {
            _updated += value;
            value?.Invoke(_current);
        }
        remove => _updated -= value;
    }

    public IReadOnlyList<TorrentSnapshot> Current => _current;

    public void Refresh()
    {
        _current = _torrentService.GetAll();
        _updated?.Invoke(_current);
    }

    private void OnUpdated(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        _current = snapshots;
        _updated?.Invoke(_current);
    }

    public void Dispose()
    {
        _torrentService.UpdateTorrent -= OnUpdated;
    }
}
