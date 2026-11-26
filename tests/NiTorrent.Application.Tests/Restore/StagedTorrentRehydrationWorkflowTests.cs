using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Restore;
using NiTorrent.Domain.Torrents;
using Xunit;

namespace NiTorrent.Application.Tests.Restore;

public sealed class StagedTorrentRehydrationWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_RehydratesAllEntries_WithoutIdentityCollapse()
    {
        var entries = new[]
        {
            CreateEntry("A", TorrentIntent.Running, DateTimeOffset.UtcNow.AddMinutes(-3)),
            CreateEntry("B", TorrentIntent.Paused, DateTimeOffset.UtcNow.AddMinutes(-2)),
            CreateEntry("C", TorrentIntent.Running, DateTimeOffset.UtcNow.AddMinutes(-1))
        };

        var repository = new InMemoryCollectionRepository(entries);
        var sourceStore = new InMemorySourceStore(entries);
        var writeService = new TrackingWriteService();
        var sut = new StagedTorrentRehydrationWorkflow(
            repository,
            sourceStore,
            writeService,
            new TestLogger<StagedTorrentRehydrationWorkflow>());

        var updated = await sut.ExecuteAsync(entries);

        Assert.Equal(3, updated.Count);
        Assert.Equal(3, writeService.RehydrateCalls.Count);
        Assert.Equal(3, writeService.RehydrateCalls.Select(x => x.Id).Distinct().Count());
        Assert.Equal(3, updated.Select(x => x.Id).Distinct().Count());
        Assert.Equal(3, updated.Select(x => x.Name).Distinct().Count());
    }

    [Fact]
    public async Task ExecuteAsync_PrioritizesRunningIntents_InDeterministicOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var pausedOld = CreateEntry("P-old", TorrentIntent.Paused, now.AddMinutes(-30));
        var runningNew = CreateEntry("R-new", TorrentIntent.Running, now.AddMinutes(-10));
        var runningOld = CreateEntry("R-old", TorrentIntent.Running, now.AddMinutes(-20));
        var pausedNew = CreateEntry("P-new", TorrentIntent.Paused, now.AddMinutes(-5));

        var input = new[] { pausedOld, runningNew, pausedNew, runningOld };
        var repository = new InMemoryCollectionRepository(input);
        var sourceStore = new InMemorySourceStore(input);
        var writeService = new TrackingWriteService();
        var sut = new StagedTorrentRehydrationWorkflow(
            repository,
            sourceStore,
            writeService,
            new TestLogger<StagedTorrentRehydrationWorkflow>());

        await sut.ExecuteAsync(input);

        var expectedOrder = new[]
        {
            runningOld.Id,
            runningNew.Id,
            pausedOld.Id,
            pausedNew.Id
        };

        Assert.Equal(expectedOrder, writeService.RehydrateCalls.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_MissingSource_DoesNotOverwriteOtherEntries()
    {
        var entries = new[]
        {
            CreateEntry("A", TorrentIntent.Running, DateTimeOffset.UtcNow.AddMinutes(-3)),
            CreateEntry("B", TorrentIntent.Paused, DateTimeOffset.UtcNow.AddMinutes(-2)),
            CreateEntry("C", TorrentIntent.Running, DateTimeOffset.UtcNow.AddMinutes(-1))
        };

        var repository = new InMemoryCollectionRepository(entries);
        var sourceStore = new InMemorySourceStore(entries, missingIds: [entries[1].Id]);
        var writeService = new TrackingWriteService();
        var sut = new StagedTorrentRehydrationWorkflow(
            repository,
            sourceStore,
            writeService,
            new TestLogger<StagedTorrentRehydrationWorkflow>());

        var updated = await sut.ExecuteAsync(entries);

        Assert.Equal(2, writeService.RehydrateCalls.Count);
        Assert.Equal(3, updated.Select(x => x.Id).Distinct().Count());
        Assert.Equal(3, updated.Select(x => x.Name).Distinct().Count());
        var missing = updated.Single(x => x.Id == entries[1].Id);
        Assert.Equal(TorrentLifecycleState.Error, missing.Runtime.LifecycleState);
    }

    private static TorrentEntry CreateEntry(string name, TorrentIntent intent, DateTimeOffset addedAtUtc)
    {
        var id = TorrentId.New();
        return new TorrentEntry(
            id,
            new TorrentKey($"k-{name}"),
            name,
            100,
            $@"C:\downloads\{name}",
            addedAtUtc,
            intent,
            TorrentLifecycleState.WaitingForEngine,
            TorrentRuntimeState.WaitingForEngine(0, false),
            new TorrentStatus(TorrentPhase.WaitingForEngine, false, 0, 0, 0),
            HasMetadata: true,
            SelectedFiles: [],
            PerTorrentSettings: null,
            DeferredActions: []);
    }

    private sealed class InMemoryCollectionRepository : ITorrentCollectionRepository
    {
        private readonly Dictionary<TorrentId, TorrentEntry> _entries;

        public InMemoryCollectionRepository(IEnumerable<TorrentEntry> entries)
            => _entries = entries.ToDictionary(x => x.Id, x => x);

        public Task<IReadOnlyList<TorrentEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TorrentEntry>>(_entries.Values.ToList());

        public Task<TorrentEntry?> TryGetAsync(TorrentId id, CancellationToken ct = default)
        {
            _entries.TryGetValue(id, out var entry);
            return Task.FromResult(entry);
        }

        public Task<TorrentEntry?> TryGetByKeyAsync(TorrentKey key, CancellationToken ct = default)
            => Task.FromResult(_entries.Values.FirstOrDefault(x => x.Key == key));

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

        public Task SaveAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class InMemorySourceStore : ITorrentSourceStore
    {
        private readonly Dictionary<TorrentId, byte[]> _byId;
        private readonly HashSet<TorrentId> _missingIds;

        public InMemorySourceStore(IEnumerable<TorrentEntry> entries, IEnumerable<TorrentId>? missingIds = null)
        {
            _byId = entries
                .Select((entry, index) => new { entry.Id, Bytes = new byte[] { (byte)(index + 1), 0xA, 0xB } })
                .ToDictionary(x => x.Id, x => x.Bytes);
            _missingIds = missingIds?.ToHashSet() ?? [];
        }

        public Task SaveAsync(TorrentId id, TorrentKey key, byte[] torrentBytes, CancellationToken ct = default)
        {
            _byId[id] = torrentBytes;
            return Task.CompletedTask;
        }

        public Task<byte[]?> TryLoadAsync(TorrentId id, TorrentKey key, CancellationToken ct = default)
        {
            if (_missingIds.Contains(id))
                return Task.FromResult<byte[]?>(null);

            _byId.TryGetValue(id, out var bytes);
            return Task.FromResult(bytes);
        }

        public Task DeleteAsync(TorrentId id, CancellationToken ct = default)
        {
            _byId.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingWriteService : ITorrentWriteService
    {
        public List<(TorrentId Id, byte SourceMarker)> RehydrateCalls { get; } = [];

        public Task<TorrentRuntimeState> AddAsync(TorrentId id, AddTorrentRequest request, CancellationToken ct = default)
            => Task.FromResult(TorrentRuntimeState.WaitingForEngine(0, false));

        public Task<TorrentRuntimeState> RehydrateAsync(TorrentEntry entry, byte[] torrentBytes, CancellationToken ct = default)
        {
            RehydrateCalls.Add((entry.Id, torrentBytes[0]));
            return Task.FromResult(new TorrentRuntimeState(
                TorrentLifecycleState.WaitingForEngine,
                false,
                0,
                0,
                0,
                null,
                true));
        }

        public Task ApplySettingsAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
