using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Read;

public sealed class TorrentReadModelFeed : ITorrentReadModelFeed
{
    private readonly ITorrentCollectionRepository _collectionRepository;
    private readonly ITorrentRuntimeFactsProvider _runtimeFactsProvider;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<TorrentSnapshot> _current = Array.Empty<TorrentSnapshot>();

    public event Action<IReadOnlyList<TorrentSnapshot>>? Updated;

    public TorrentReadModelFeed(
        ITorrentCollectionRepository collectionRepository,
        ITorrentRuntimeFactsProvider runtimeFactsProvider)
    {
        _collectionRepository = collectionRepository;
        _runtimeFactsProvider = runtimeFactsProvider;
        _runtimeFactsProvider.RuntimeFactsUpdated += OnRuntimeFactsUpdated;
    }

    public IReadOnlyList<TorrentSnapshot> GetCurrent() => _current;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await _refreshGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = await _collectionRepository.GetAllAsync(ct).ConfigureAwait(false);
            var runtimeFacts = _runtimeFactsProvider.GetAll();
            var projected = Project(entries, runtimeFacts);
            _current = projected;
            Updated?.Invoke(projected);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void OnRuntimeFactsUpdated(IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
    {
        _ = RefreshFromFactsAsync(runtimeFacts);
    }

    private async Task RefreshFromFactsAsync(IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
    {
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var entries = await _collectionRepository.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
            var projected = Project(entries, runtimeFacts);
            _current = projected;
            Updated?.Invoke(projected);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static IReadOnlyList<TorrentSnapshot> Project(
        IReadOnlyList<TorrentEntry> entries,
        IReadOnlyList<TorrentRuntimeFact> runtimeFacts)
    {
        var runtimeById = runtimeFacts
            .Where(x => x.Id is not null)
            .GroupBy(x => x.Id!)
            .ToDictionary(g => g.Key, g => g.Last());

        var runtimeByKey = runtimeFacts
            .Where(x => !x.Key.IsEmpty)
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.Last());

        return entries
            .Select(entry =>
            {
                runtimeById.TryGetValue(entry.Id, out var runtimeFact);
                if (runtimeFact is null && !entry.Key.IsEmpty)
                    runtimeByKey.TryGetValue(entry.Key, out runtimeFact);

                return TorrentReadModelProjectionPolicy.Project(entry, runtimeFact);
            })
            .OrderByDescending(x => x.AddedAtUtc)
            .ToList();
    }
}
