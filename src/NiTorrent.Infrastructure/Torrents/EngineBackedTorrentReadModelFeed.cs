using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Deferred;
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
    private readonly ReplayDeferredTorrentActionsWorkflow _replayDeferredActionsWorkflow;
    private readonly ILogger<EngineBackedTorrentReadModelFeed> _logger;
    private readonly object _sync = new();
    private long _syncCycleId;
    private IReadOnlyList<TorrentListItemReadModel> _current = [];
    private event Action<IReadOnlyList<TorrentListItemReadModel>>? _updated;

    public EngineBackedTorrentReadModelFeed(
        GetTorrentListQuery getTorrentListQuery,
        ITorrentRuntimeFactsProvider runtimeFactsProvider,
        SyncTorrentCollectionFromRuntimeWorkflow syncRuntimeWorkflow,
        ReplayDeferredTorrentActionsWorkflow replayDeferredActionsWorkflow,
        ILogger<EngineBackedTorrentReadModelFeed> logger)
    {
        _getTorrentListQuery = getTorrentListQuery;
        _runtimeFactsProvider = runtimeFactsProvider;
        _syncRuntimeWorkflow = syncRuntimeWorkflow;
        _replayDeferredActionsWorkflow = replayDeferredActionsWorkflow;
        _logger = logger;

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
        => _ = SynchronizeAndRefreshAsync(runtimeFacts);

    private async Task SynchronizeAndRefreshAsync(IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
    {
        var cycleId = Interlocked.Increment(ref _syncCycleId);
        var affectedCount = runtimeFacts.Count;
        var affectedIds = string.Join(", ", runtimeFacts
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value.ToString())
            .Distinct()
            .Take(5));

        try
        {
            await _syncRuntimeWorkflow.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Read sync cycle {CycleId} failed at runtime synchronization (affectedFacts={AffectedCount}, sampleIds={AffectedIds})",
                cycleId,
                affectedCount,
                string.IsNullOrWhiteSpace(affectedIds) ? "<none>" : affectedIds);
        }

        try
        {
            await _replayDeferredActionsWorkflow
                .ExecuteAsync(trigger: "read-feed-runtime-resync", ct: CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Read sync cycle {CycleId} failed at deferred replay stage (affectedFacts={AffectedCount}, sampleIds={AffectedIds})",
                cycleId,
                affectedCount,
                string.IsNullOrWhiteSpace(affectedIds) ? "<none>" : affectedIds);
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
