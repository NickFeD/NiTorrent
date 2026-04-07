using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Restore;
using NiTorrent.Domain.Torrents;
using Xunit;

namespace NiTorrent.Application.Tests.Restore;

public sealed class SyncTorrentCollectionFromRuntimeWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_WhenNoEffectiveChanges_SkipsUpsert_AndSkipsSave()
    {
        var entry = CreateEntry(TorrentIntent.Paused, TorrentLifecycleState.Paused, progress: 0, downloadRate: 0, uploadRate: 0, isEngineBacked: false);
        var repository = new TrackingCollectionRepository([entry]);
        var runtimeFactsProvider = new FixedRuntimeFactsProvider([]);
        var sut = new SyncTorrentCollectionFromRuntimeWorkflow(repository, runtimeFactsProvider);

        await sut.ExecuteAsync();

        Assert.Equal(0, repository.UpsertCalls);
        Assert.Empty(repository.SaveForceFlags);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyRuntimeTelemetryChanges_UpsertsChangedEntries_AndSkipsSave()
    {
        var entry = CreateEntry(TorrentIntent.Running, TorrentLifecycleState.WaitingForEngine, progress: 10, downloadRate: 0, uploadRate: 0, isEngineBacked: false);
        var runtime = new TorrentRuntimeFact(
            entry.Id,
            entry.Key,
            entry.Name,
            entry.Size,
            entry.SavePath,
            new TorrentRuntimeState(
                TorrentLifecycleState.Downloading,
                IsComplete: false,
                Progress: 25,
                DownloadRateBytesPerSecond: 1024,
                UploadRateBytesPerSecond: 64,
                Error: null,
                IsEngineBacked: true));

        var repository = new TrackingCollectionRepository([entry]);
        var runtimeFactsProvider = new FixedRuntimeFactsProvider([runtime]);
        var sut = new SyncTorrentCollectionFromRuntimeWorkflow(repository, runtimeFactsProvider);

        await sut.ExecuteAsync();

        Assert.Equal(1, repository.UpsertCalls);
        Assert.Empty(repository.SaveForceFlags);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDurableFieldsChange_UpsertsChangedEntries_AndUsesNonForcedSave()
    {
        var id = TorrentId.New();
        var entry = new TorrentEntry(
            id,
            TorrentKey.Empty,
            string.Empty,
            0,
            string.Empty,
            DateTimeOffset.UtcNow,
            TorrentIntent.Running,
            TorrentLifecycleState.WaitingForEngine,
            new TorrentRuntimeState(
                TorrentLifecycleState.WaitingForEngine,
                IsComplete: false,
                Progress: 0,
                DownloadRateBytesPerSecond: 0,
                UploadRateBytesPerSecond: 0,
                Error: null,
                IsEngineBacked: false),
            new TorrentStatus(TorrentPhase.WaitingForEngine, false, 0, 0, 0),
            HasMetadata: false,
            SelectedFiles: [],
            PerTorrentSettings: null,
            DeferredActions: []);

        var runtime = new TorrentRuntimeFact(
            id,
            new TorrentKey($"k-{id.Value:N}"),
            "updated-name",
            1024,
            "C:\\downloads",
            new TorrentRuntimeState(
                TorrentLifecycleState.Downloading,
                IsComplete: false,
                Progress: 1,
                DownloadRateBytesPerSecond: 256,
                UploadRateBytesPerSecond: 32,
                Error: null,
                IsEngineBacked: true));

        var repository = new TrackingCollectionRepository([entry]);
        var runtimeFactsProvider = new FixedRuntimeFactsProvider([runtime]);
        var sut = new SyncTorrentCollectionFromRuntimeWorkflow(repository, runtimeFactsProvider);

        await sut.ExecuteAsync();

        Assert.Equal(1, repository.UpsertCalls);
        Assert.Single(repository.SaveForceFlags);
        Assert.False(repository.SaveForceFlags[0]);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleEntries_DoesNotCollapseIdentityAfterRuntimeSync()
    {
        var a = CreateEntry(TorrentIntent.Running, TorrentLifecycleState.WaitingForEngine, 0, 0, 0, false);
        var b = CreateEntry(TorrentIntent.Running, TorrentLifecycleState.WaitingForEngine, 0, 0, 0, false);
        var c = CreateEntry(TorrentIntent.Paused, TorrentLifecycleState.Paused, 0, 0, 0, false);

        var runtimeFacts = new[]
        {
            new TorrentRuntimeFact(
                a.Id,
                a.Key,
                "A-runtime",
                a.Size,
                a.SavePath,
                new TorrentRuntimeState(TorrentLifecycleState.Downloading, false, 12, 111, 11, null, true)),
            new TorrentRuntimeFact(
                b.Id,
                b.Key,
                "B-runtime",
                b.Size,
                b.SavePath,
                new TorrentRuntimeState(TorrentLifecycleState.Seeding, true, 100, 0, 222, null, true)),
            new TorrentRuntimeFact(
                c.Id,
                c.Key,
                "C-runtime",
                c.Size,
                c.SavePath,
                new TorrentRuntimeState(TorrentLifecycleState.Downloading, false, 80, 333, 33, null, true))
        };

        var repository = new TrackingCollectionRepository([a, b, c]);
        var runtimeFactsProvider = new FixedRuntimeFactsProvider(runtimeFacts);
        var sut = new SyncTorrentCollectionFromRuntimeWorkflow(repository, runtimeFactsProvider);

        var result = await sut.ExecuteAsync();

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(3, result.Entries.Select(x => x.Id).Distinct().Count());

        var byId = result.Entries.ToDictionary(x => x.Id, x => x);
        Assert.Equal("A-runtime", byId[a.Id].Name);
        Assert.Equal("B-runtime", byId[b.Id].Name);
        Assert.Equal("C-runtime", byId[c.Id].Name);
    }

    private static TorrentEntry CreateEntry(
        TorrentIntent intent,
        TorrentLifecycleState lifecycleState,
        double progress,
        long downloadRate,
        long uploadRate,
        bool isEngineBacked)
    {
        var id = TorrentId.New();
        var runtime = new TorrentRuntimeState(
            lifecycleState,
            IsComplete: false,
            Progress: progress,
            DownloadRateBytesPerSecond: downloadRate,
            UploadRateBytesPerSecond: uploadRate,
            Error: null,
            IsEngineBacked: isEngineBacked);

        return new TorrentEntry(
            id,
            new TorrentKey($"k-{id.Value:N}"),
            "torrent",
            1024,
            "C:\\downloads",
            DateTimeOffset.UtcNow,
            intent,
            lifecycleState,
            runtime,
            new TorrentStatus(
                TorrentLifecycleStateMapper.ToPhase(lifecycleState),
                false,
                progress,
                downloadRate,
                uploadRate,
                Error: null,
                Source: isEngineBacked ? TorrentStatusSource.Live : TorrentStatusSource.Cached),
            HasMetadata: true,
            SelectedFiles: [],
            PerTorrentSettings: null,
            DeferredActions: []);
    }

    private sealed class FixedRuntimeFactsProvider(IReadOnlyList<TorrentRuntimeFact> facts) : ITorrentRuntimeFactsProvider
    {
        public IReadOnlyList<TorrentRuntimeFact> GetAll() => facts;
        public event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;
    }

    private sealed class TrackingCollectionRepository : ITorrentCollectionRepository
    {
        private readonly Dictionary<TorrentId, TorrentEntry> _entries;

        public int UpsertCalls { get; private set; }
        public List<bool> SaveForceFlags { get; } = [];

        public TrackingCollectionRepository(IEnumerable<TorrentEntry> entries)
            => _entries = entries.ToDictionary(x => x.Id, x => x);

        public Task<IReadOnlyList<TorrentEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TorrentEntry>>(_entries.Values.ToList());

        public Task<TorrentEntry?> TryGetAsync(TorrentId id, CancellationToken ct = default)
        {
            _entries.TryGetValue(id, out var value);
            return Task.FromResult(value);
        }

        public Task<TorrentEntry?> TryGetByKeyAsync(TorrentKey key, CancellationToken ct = default)
            => Task.FromResult(_entries.Values.FirstOrDefault(x => x.Key == key));

        public Task UpsertAsync(TorrentEntry entry, CancellationToken ct = default)
        {
            UpsertCalls++;
            _entries[entry.Id] = entry;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(TorrentId id, CancellationToken ct = default)
        {
            _entries.Remove(id);
            return Task.CompletedTask;
        }

        public Task SaveAsync(bool force = true, CancellationToken ct = default)
        {
            SaveForceFlags.Add(force);
            return Task.CompletedTask;
        }
    }
}
