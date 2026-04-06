using Microsoft.Extensions.Logging.Abstractions;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Deferred;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Application.Torrents.Restore;
using NiTorrent.Domain.Torrents;
using NiTorrent.Infrastructure.Torrents;
using Xunit;

namespace NiTorrent.Infrastructure.Tests.Torrents;

public sealed class EngineBackedTorrentReadModelFeedTests
{
    [Fact]
    public async Task Refresh_WhenProjectionIsUnchanged_DoesNotPublishDuplicateUpdatedEvent()
    {
        var entry = CreateEntry();
        var repository = new InMemoryRepository([entry]);
        var runtimeFacts = new StubRuntimeFactsProvider();
        var syncWorkflow = new SyncTorrentCollectionFromRuntimeWorkflow(repository, runtimeFacts);
        var replayWorkflow = new ReplayDeferredTorrentActionsWorkflow(
            repository,
            new ApplyDeferredTorrentActionsWorkflow(new NoOpEngineGateway()),
            NullLogger<ReplayDeferredTorrentActionsWorkflow>.Instance);
        var query = new GetTorrentListQuery(repository);

        using var feed = new EngineBackedTorrentReadModelFeed(
            query,
            runtimeFacts,
            syncWorkflow,
            replayWorkflow,
            NullLogger<EngineBackedTorrentReadModelFeed>.Instance);

        await WaitUntilAsync(() => feed.Current.Count == 1, TimeSpan.FromSeconds(2));

        var updateEvents = 0;
        feed.Updated += _ => updateEvents++;

        feed.Refresh();
        feed.Refresh();

        await Task.Delay(200);

        Assert.Equal(1, updateEvents);
    }

    private static TorrentEntry CreateEntry()
    {
        var id = TorrentId.New();
        return new TorrentEntry(
            id,
            new TorrentKey($"k-{id.Value:N}"),
            "torrent",
            1024,
            "C:\\downloads",
            DateTimeOffset.UtcNow,
            TorrentIntent.Running,
            TorrentLifecycleState.Downloading,
            new TorrentRuntimeState(
                TorrentLifecycleState.Downloading,
                IsComplete: false,
                Progress: 12.5,
                DownloadRateBytesPerSecond: 2048,
                UploadRateBytesPerSecond: 128,
                Error: null,
                IsEngineBacked: true),
            new TorrentStatus(
                TorrentPhase.Downloading,
                IsComplete: false,
                Progress: 12.5,
                DownloadRateBytesPerSecond: 2048,
                UploadRateBytesPerSecond: 128,
                Error: null,
                Source: TorrentStatusSource.Live),
            HasMetadata: true,
            SelectedFiles: [],
            PerTorrentSettings: null,
            DeferredActions: []);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - started > timeout)
                throw new TimeoutException("Condition wait timed out.");

            await Task.Delay(25);
        }
    }

    private sealed class StubRuntimeFactsProvider : ITorrentRuntimeFactsProvider
    {
        public event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;

        public IReadOnlyList<TorrentRuntimeFact> GetAll() => Array.Empty<TorrentRuntimeFact>();

        public void Raise(IReadOnlyList<TorrentRuntimeFact> facts)
            => RuntimeFactsUpdated?.Invoke(facts);
    }

    private sealed class NoOpEngineGateway : ITorrentEngineGateway
    {
        public Task<bool> StartAsync(TorrentId id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> PauseAsync(TorrentId id, CancellationToken ct = default) => Task.FromResult(false);
        public Task StopAsync(TorrentId id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class InMemoryRepository : ITorrentCollectionRepository
    {
        private readonly Dictionary<TorrentId, TorrentEntry> _entries;

        public InMemoryRepository(IEnumerable<TorrentEntry> entries)
        {
            _entries = entries.ToDictionary(x => x.Id, x => x);
        }

        public Task<IReadOnlyList<TorrentEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TorrentEntry>>(_entries.Values.ToList());

        public Task<TorrentEntry?> TryGetAsync(TorrentId id, CancellationToken ct = default)
        {
            _entries.TryGetValue(id, out var value);
            return Task.FromResult(value);
        }

        public Task<TorrentEntry?> TryGetByKeyAsync(TorrentKey key, CancellationToken ct = default)
        {
            var found = _entries.Values.FirstOrDefault(x => x.Key == key);
            return Task.FromResult(found);
        }

        public Task UpsertAsync(TorrentEntry entry, CancellationToken ct = default)
        {
            _entries[entry.Id] = entry;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(TorrentId id, CancellationToken ct = default)
        {
            _entries.Remove(id);
            return Task.CompletedTask;
        }

        public Task SaveAsync(bool force = true, CancellationToken ct = default) => Task.CompletedTask;
    }
}
