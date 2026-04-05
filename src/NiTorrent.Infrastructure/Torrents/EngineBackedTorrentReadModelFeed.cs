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
    private readonly object _workSync = new();
    private long _syncCycleId;
    private IReadOnlyList<TorrentListItemReadModel> _current = [];
    private IReadOnlyList<TorrentRuntimeFact> _latestRuntimeFacts = Array.Empty<TorrentRuntimeFact>();
    private bool _refreshRequested;
    private bool _runtimeSyncRequested;
    private bool _isProcessing;
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
        => QueueWork(refreshRequested: true, runtimeSyncRequested: false, runtimeFacts: null);

    private void QueueWork(bool refreshRequested, bool runtimeSyncRequested, IReadOnlyList<TorrentRuntimeFact>? runtimeFacts)
    {
        var shouldStart = false;
        lock (_workSync)
        {
            _refreshRequested |= refreshRequested;
            _runtimeSyncRequested |= runtimeSyncRequested;
            if (runtimeFacts is not null)
                _latestRuntimeFacts = runtimeFacts;

            if (!_isProcessing)
            {
                _isProcessing = true;
                shouldStart = true;
            }
        }

        if (shouldStart)
            _ = ProcessWorkAsync();
    }

    private void OnRuntimeFactsUpdated(IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
        => QueueWork(refreshRequested: true, runtimeSyncRequested: true, runtimeFacts: runtimeFacts);

    private async Task ProcessWorkAsync()
    {
        while (true)
        {
            bool shouldRefresh;
            bool shouldSyncRuntime;
            IReadOnlyList<TorrentRuntimeFact> runtimeFacts;

            lock (_workSync)
            {
                shouldRefresh = _refreshRequested;
                shouldSyncRuntime = _runtimeSyncRequested;
                runtimeFacts = _latestRuntimeFacts;

                _refreshRequested = false;
                _runtimeSyncRequested = false;
                _latestRuntimeFacts = Array.Empty<TorrentRuntimeFact>();
            }

            try
            {
                if (shouldSyncRuntime)
                    await SynchronizeRuntimeAsync(runtimeFacts).ConfigureAwait(false);

                if (shouldRefresh || shouldSyncRuntime)
                    await RefreshCoreAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Read feed processing iteration failed");
            }

            lock (_workSync)
            {
                if (!_refreshRequested && !_runtimeSyncRequested)
                {
                    _isProcessing = false;
                    return;
                }
            }
        }
    }

    private async Task RefreshCoreAsync()
    {
        var items = await _getTorrentListQuery.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        OnUpdated(items);
    }

    private async Task SynchronizeRuntimeAsync(IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
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
    }

    private void OnUpdated(IReadOnlyList<TorrentListItemReadModel> items)
    {
        bool changed;
        lock (_sync)
        {
            changed = !AreSame(_current, items);
            if (!changed)
                return;

            _current = items;
        }

        _updated?.Invoke(items);
    }

    private static bool AreSame(IReadOnlyList<TorrentListItemReadModel> left, IReadOnlyList<TorrentListItemReadModel> right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!EqualityComparer<TorrentListItemReadModel>.Default.Equals(left[i], right[i]))
                return false;
        }

        return true;
    }

    public void Dispose()
    {
        _runtimeFactsProvider.RuntimeFactsUpdated -= OnRuntimeFactsUpdated;
    }
}
