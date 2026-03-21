using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentReadModelFeed : ITorrentReadModelFeed, IDisposable
{
    private readonly ITorrentService _torrentService;
    private event Action<IReadOnlyList<TorrentSnapshot>>? _updated;
    private IReadOnlyList<TorrentSnapshot> _current;

    public TorrentReadModelFeed(ITorrentService torrentService)
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
            if (value is not null)
                value(_current);
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
