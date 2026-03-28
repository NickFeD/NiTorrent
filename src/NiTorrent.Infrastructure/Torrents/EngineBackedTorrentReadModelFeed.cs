using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Application.Torrents.Restore;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Read feed that projects UI read models from the synchronized product-owned torrent collection.
/// Runtime events trigger synchronization through the application workflow before the feed is refreshed.
/// </summary>
public sealed class EngineBackedTorrentReadModelFeed : ITorrentReadModelFeed, IDisposable
{
    private readonly GetTorrentListQuery _getTorrentListQuery;
    private readonly ITorrentRuntimeFactsProvider _runtimeFactsProvider;
    private readonly SyncTorrentCollectionFromRuntimeWorkflow _syncRuntimeWorkflow;
    private readonly object _sync = new();
    private IReadOnlyList<TorrentListItemReadModel> _current = [];
    private event Action<IReadOnlyList<TorrentListItemReadModel>>? _updated;

    public EngineBackedTorrentReadModelFeed(
        GetTorrentListQuery getTorrentListQuery,
        ITorrentRuntimeFactsProvider runtimeFactsProvider,
        SyncTorrentCollectionFromRuntimeWorkflow syncRuntimeWorkflow)
    {
        _getTorrentListQuery = getTorrentListQuery;
        _runtimeFactsProvider = runtimeFactsProvider;
        _syncRuntimeWorkflow = syncRuntimeWorkflow;

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

    private void OnRuntimeFactsUpdated(IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
        => _ = SynchronizeAndRefreshAsync();

    private async Task SynchronizeAndRefreshAsync()
    {
        try
        {
            await _syncRuntimeWorkflow.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort synchronization; still try to show the latest persisted projection.
        }

        await RefreshAsync().ConfigureAwait(false);
    }

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
