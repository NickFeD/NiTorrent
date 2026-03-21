using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Restore;

public sealed class RestoreTorrentCollectionWorkflow : IRestoreTorrentCollectionWorkflow
{
    private readonly ITorrentCollectionRepository _repository;
    private readonly ITorrentEngineLifecycle _engineLifecycle;
    private readonly ITorrentRuntimeFactsProvider _runtimeFactsProvider;
    private readonly ITorrentEngineGateway _engineGateway;

    public RestoreTorrentCollectionWorkflow(
        ITorrentCollectionRepository repository,
        ITorrentEngineLifecycle engineLifecycle,
        ITorrentRuntimeFactsProvider runtimeFactsProvider,
        ITorrentEngineGateway engineGateway)
    {
        _repository = repository;
        _engineLifecycle = engineLifecycle;
        _runtimeFactsProvider = runtimeFactsProvider;
        _engineGateway = engineGateway;
    }

    public async Task<RestoreTorrentCollectionResult> ExecuteAsync(CancellationToken ct = default)
    {
        var earlyCollection = await _repository.GetAllAsync(ct).ConfigureAwait(false);

        await _engineLifecycle.InitializeAsync(ct).ConfigureAwait(false);
        var runtimeFacts = _runtimeFactsProvider.GetAll();

        var syncedCollection = TorrentCollectionRestorePolicy.ApplyRuntimeFacts(earlyCollection, runtimeFacts).ToList();
        syncedCollection = await ApplyIntentAndDeferredActionsAsync(syncedCollection, ct).ConfigureAwait(false);

        foreach (var entry in syncedCollection)
        {
            await _repository.UpsertAsync(entry, ct).ConfigureAwait(false);
        }
        await _repository.SaveAsync(ct).ConfigureAwait(false);

        return new RestoreTorrentCollectionResult(earlyCollection, syncedCollection, runtimeFacts);
    }

    private async Task<List<TorrentEntry>> ApplyIntentAndDeferredActionsAsync(List<TorrentEntry> entries, CancellationToken ct)
    {
        foreach (var entry in entries.ToList())
        {
            if (entry.Intent == TorrentIntent.Running)
            {
                await _engineGateway.StartAsync(entry.Id, ct).ConfigureAwait(false);
            }
            else if (entry.Intent == TorrentIntent.Paused)
            {
                await _engineGateway.PauseAsync(entry.Id, ct).ConfigureAwait(false);
            }

            foreach (var action in entry.DeferredActions.OrderBy(x => x.RequestedAtUtc))
            {
                switch (action.Type)
                {
                    case DeferredActionType.Start:
                        await _engineGateway.StartAsync(entry.Id, ct).ConfigureAwait(false);
                        entry = entry.WithIntent(TorrentIntent.Running);
                        break;
                    case DeferredActionType.Pause:
                        await _engineGateway.PauseAsync(entry.Id, ct).ConfigureAwait(false);
                        entry = entry.WithIntent(TorrentIntent.Paused);
                        break;
                    case DeferredActionType.RemoveKeepData:
                        await _engineGateway.RemoveAsync(entry.Id, deleteData: false, ct).ConfigureAwait(false);
                        entry = entry.WithIntent(TorrentIntent.Removed);
                        break;
                    case DeferredActionType.RemoveWithData:
                        await _engineGateway.RemoveAsync(entry.Id, deleteData: true, ct).ConfigureAwait(false);
                        entry = entry.WithIntent(TorrentIntent.Removed);
                        break;
                }
            }

            entry = entry.WithDeferredActions(Array.Empty<DeferredAction>());
            var index = entries.FindIndex(x => x.Id == entry.Id);
            if (index >= 0)
                entries[index] = entry;
        }

        entries.RemoveAll(x => x.Intent == TorrentIntent.Removed);
        return entries;
    }
}
