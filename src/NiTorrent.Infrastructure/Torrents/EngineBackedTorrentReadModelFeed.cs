using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Read feed that projects UI read models from the product-owned torrent collection plus runtime facts.
/// Infrastructure runtime events only invalidate the projection; they do not publish UI truth directly.
/// </summary>
public sealed class EngineBackedTorrentReadModelFeed : ITorrentReadModelFeed, IDisposable
{
    private readonly GetTorrentListQuery _getTorrentListQuery;
    private readonly ITorrentRuntimeFactsProvider _runtimeFactsProvider;
    private readonly object _sync = new();
    private IReadOnlyList<TorrentListItemReadModel> _current = [];
    private event Action<IReadOnlyList<TorrentListItemReadModel>>? _updated;

    public EngineBackedTorrentReadModelFeed(
        GetTorrentListQuery getTorrentListQuery,
        ITorrentRuntimeFactsProvider runtimeFactsProvider)
    {
        _getTorrentListQuery = getTorrentListQuery;
        _runtimeFactsProvider = runtimeFactsProvider;

        _runtimeFactsProvider.RuntimeFactsUpdated += OnRuntimeFactsUpdated;
        Refresh();
    }

    public event Action<IReadOnlyList<TorrentListItemReadModel>>? Updated
    {
        add
        {
            _updated += value;
            value?.Invoke(Current);
        }
        remove => _updated -= value;
    }

    public IReadOnlyList<TorrentListItemReadModel> Current
    {
        get
        {
            lock (_sync)
                return _current;
        }
    }

    public void Refresh()
        => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        var items = await _getTorrentListQuery.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        OnUpdated(items);
    }

    private void OnRuntimeFactsUpdated(IReadOnlyList<TorrentRuntimeFact> _)
        => Refresh();

    private void OnUpdated(IReadOnlyList<TorrentListItemReadModel> items)
    {
        lock (_sync)
            _current = items;

        _updated?.Invoke(items);
    }

    public void Dispose()
    {
        _runtimeFactsProvider.RuntimeFactsUpdated -= OnRuntimeFactsUpdated;
    }
}
