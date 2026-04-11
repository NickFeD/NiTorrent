using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;
using Xunit;

namespace NiTorrent.Application.Tests.Torrents;

public sealed class AddTorrentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDuplicateExists_ReturnsAlreadyExists()
    {
        var existing = CreateEntry();
        var repository = new InMemoryCollectionRepository([existing]);
        var sourceStore = new InMemorySourceStore();
        var writeService = new StubWriteService();
        var sut = new AddTorrentUseCase(repository, writeService, sourceStore);

        var request = CreateRequest(existing.Key, existing.Name, existing.SavePath, [1, 2, 3]);

        var result = await sut.ExecuteAsync(request);

        Assert.Equal(AddTorrentOutcome.AlreadyExists, result.Outcome);
        Assert.Equal(1, repository.Entries.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSourceSaveFails_RollsBackCatalogEntry()
    {
        var repository = new InMemoryCollectionRepository([]);
        var sourceStore = new InMemorySourceStore(throwOnSave: true);
        var writeService = new StubWriteService();
        var sut = new AddTorrentUseCase(repository, writeService, sourceStore);

        var result = await sut.ExecuteAsync(CreateRequest(new TorrentKey("k-1"), "torrent-1", "C:\\downloads", [4, 5, 6]));

        Assert.Equal(AddTorrentOutcome.StorageError, result.Outcome);
        Assert.Empty(repository.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRuntimeAddFails_PersistsDeferredStartAndReturnsSuccess()
    {
        var repository = new InMemoryCollectionRepository([]);
        var sourceStore = new InMemorySourceStore();
        var writeService = new StubWriteService(throwOnAdd: true);
        var sut = new AddTorrentUseCase(repository, writeService, sourceStore);

        var result = await sut.ExecuteAsync(CreateRequest(new TorrentKey("k-2"), "torrent-2", "C:\\downloads", [7, 8, 9]));

        Assert.Equal(AddTorrentOutcome.Success, result.Outcome);
        Assert.NotNull(result.TorrentId);

        var stored = await repository.TryGetAsync(result.TorrentId!.Value);
        Assert.NotNull(stored);
        Assert.Contains(stored!.DeferredActions, x => x.Type == DeferredActionType.Start);
    }

    private static AddTorrentRequest CreateRequest(TorrentKey key, string name, string savePath, byte[] bytes)
        => new(
            new PreparedTorrentSource(
                bytes,
                key,
                name,
                TotalSize: 100,
                Files: [],
                HasMetadata: true),
            savePath,
            SelectedFilePaths: null);

    private static TorrentEntry CreateEntry()
    {
        var id = TorrentId.New();
        return new TorrentEntry(
            id,
            new TorrentKey($"k-{id.Value:N}"),
            "existing",
            100,
            "C:\\downloads",
            DateTimeOffset.UtcNow,
            TorrentIntent.Running,
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

        public IReadOnlyDictionary<TorrentId, TorrentEntry> Entries => _entries;

        public InMemoryCollectionRepository(IEnumerable<TorrentEntry> entries)
            => _entries = entries.ToDictionary(x => x.Id, x => x);

        public Task<IReadOnlyList<TorrentEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TorrentEntry>>(_entries.Values.ToList());

        public Task<TorrentEntry?> TryGetAsync(TorrentId id, CancellationToken ct = default)
            => Task.FromResult(_entries.TryGetValue(id, out var entry) ? entry : null);

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

    private sealed class InMemorySourceStore(bool throwOnSave = false) : ITorrentSourceStore
    {
        private readonly Dictionary<TorrentId, byte[]> _sources = [];

        public Task SaveAsync(TorrentId id, TorrentKey key, byte[] torrentBytes, CancellationToken ct = default)
        {
            if (throwOnSave)
                throw new IOException("source save failed");

            _sources[id] = torrentBytes;
            return Task.CompletedTask;
        }

        public Task<byte[]?> TryLoadAsync(TorrentId id, TorrentKey key, CancellationToken ct = default)
            => Task.FromResult<byte[]?>(_sources.TryGetValue(id, out var bytes) ? bytes : null);

        public Task DeleteAsync(TorrentId id, CancellationToken ct = default)
        {
            _sources.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class StubWriteService(bool throwOnAdd = false) : ITorrentWriteService
    {
        public Task<TorrentRuntimeState> AddAsync(TorrentId id, AddTorrentRequest request, CancellationToken ct = default)
        {
            if (throwOnAdd)
                throw new InvalidOperationException("engine unavailable");

            return Task.FromResult(new TorrentRuntimeState(
                TorrentLifecycleState.Downloading,
                false,
                1,
                100,
                10,
                null,
                true));
        }

        public Task<TorrentRuntimeState> RehydrateAsync(TorrentEntry entry, byte[] torrentBytes, CancellationToken ct = default)
            => Task.FromResult(entry.Runtime);

        public Task ApplySettingsAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
