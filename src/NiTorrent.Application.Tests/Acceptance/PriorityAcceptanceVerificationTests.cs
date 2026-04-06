using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.Deferred;
using NiTorrent.Application.Torrents.Restore;
using NiTorrent.Domain.Torrents;
using Xunit;

namespace NiTorrent.Application.Tests.Acceptance;

public sealed class PriorityAcceptanceVerificationTests
{
    [Fact]
    public async Task CommandsBeforeEngineReadiness_StartIsDeferred_AndStored()
    {
        var entry = CreateEntry(TorrentIntent.Paused, TorrentLifecycleState.Paused);
        var repository = new InMemoryCollectionRepository([entry]);
        var gateway = new ControlledGateway(startResult: false, pauseResult: true, removeResult: true);
        var sut = new TorrentCommandService(repository, gateway);

        var result = await sut.StartAsync(entry.Id);
        var stored = await repository.TryGetAsync(entry.Id);

        Assert.NotNull(stored);
        Assert.Equal(TorrentCommandOutcome.Deferred, result.Outcome);
        Assert.Contains(stored!.DeferredActions, x => x.Type == DeferredActionType.Start);
        Assert.True(repository.SaveCalls >= 1);
    }

    [Fact]
    public async Task StartupRestore_RunningIntent_ReplaysDeferredStart()
    {
        var entry = CreateEntry(TorrentIntent.Running, TorrentLifecycleState.WaitingForEngine);
        var repository = new InMemoryCollectionRepository([entry]);
        var runtimeFactsProvider = new FixedRuntimeFactsProvider([]);
        var sync = new SyncTorrentCollectionFromRuntimeWorkflow(repository, runtimeFactsProvider);
        var gateway = new ControlledGateway(startResult: true, pauseResult: true, removeResult: true);
        var apply = new ApplyDeferredTorrentActionsWorkflow(gateway);
        var replay = new ReplayDeferredTorrentActionsWorkflow(
            repository,
            apply,
            new TestLogger<ReplayDeferredTorrentActionsWorkflow>());
        var writeService = new NoopWriteService();
        var staged = new StagedTorrentRehydrationWorkflow(
            repository,
            new InMemorySourceStore(),
            writeService,
            new TestLogger<StagedTorrentRehydrationWorkflow>());
        var lifecycle = new TrackingEngineLifecycle();
        var restore = new RestoreTorrentCollectionWorkflow(
            repository,
            lifecycle,
            writeService,
            staged,
            sync,
            replay,
            new NoopLegacyMigrationSource());

        var result = await restore.ExecuteAsync();
        var restored = Assert.Single(result.RestoredCollection);

        Assert.Equal(1, lifecycle.InitializeCalls);
        Assert.Equal(1, gateway.StartCalls);
        Assert.DoesNotContain(restored.DeferredActions, x => x.Type == DeferredActionType.Start);
        Assert.Equal(TorrentIntent.Running, restored.Intent);
    }

    [Fact]
    public async Task StateMappingAfterRestart_PausedIntent_RemainsPaused_AfterRuntimeSync()
    {
        var entry = CreateEntry(TorrentIntent.Paused, TorrentLifecycleState.Paused);
        var runtimeFacts = new[]
        {
            new TorrentRuntimeFact(
                entry.Id,
                entry.Key,
                entry.Name,
                entry.Size,
                entry.SavePath,
                new TorrentRuntimeState(
                    TorrentLifecycleState.Downloading,
                    IsComplete: false,
                    Progress: 42,
                    DownloadRateBytesPerSecond: 1024,
                    UploadRateBytesPerSecond: 128,
                    Error: null,
                    IsEngineBacked: true))
        };

        var repository = new InMemoryCollectionRepository([entry]);
        var runtimeFactsProvider = new FixedRuntimeFactsProvider(runtimeFacts);
        var sync = new SyncTorrentCollectionFromRuntimeWorkflow(repository, runtimeFactsProvider);
        var gateway = new ControlledGateway(startResult: true, pauseResult: true, removeResult: true);
        var apply = new ApplyDeferredTorrentActionsWorkflow(gateway);
        var replay = new ReplayDeferredTorrentActionsWorkflow(
            repository,
            apply,
            new TestLogger<ReplayDeferredTorrentActionsWorkflow>());
        var writeService = new NoopWriteService();
        var staged = new StagedTorrentRehydrationWorkflow(
            repository,
            new InMemorySourceStore(),
            writeService,
            new TestLogger<StagedTorrentRehydrationWorkflow>());
        var lifecycle = new TrackingEngineLifecycle();
        var restore = new RestoreTorrentCollectionWorkflow(
            repository,
            lifecycle,
            writeService,
            staged,
            sync,
            replay,
            new NoopLegacyMigrationSource());

        var result = await restore.ExecuteAsync();
        var restored = Assert.Single(result.RestoredCollection);

        Assert.Equal(TorrentIntent.Paused, restored.Intent);
        Assert.Equal(TorrentLifecycleState.Paused, restored.Runtime.LifecycleState);
        Assert.Equal(0, restored.Runtime.DownloadRateBytesPerSecond);
        Assert.Equal(0, restored.Runtime.UploadRateBytesPerSecond);
    }

    private static TorrentEntry CreateEntry(TorrentIntent intent, TorrentLifecycleState lifecycle)
    {
        var id = TorrentId.New();
        var runtime = new TorrentRuntimeState(
            lifecycle,
            IsComplete: false,
            Progress: 0,
            DownloadRateBytesPerSecond: 0,
            UploadRateBytesPerSecond: 0,
            Error: null,
            IsEngineBacked: false);

        return new TorrentEntry(
            id,
            new TorrentKey($"k-{id.Value:N}"),
            "torrent",
            100,
            "C:\\downloads",
            DateTimeOffset.UtcNow,
            intent,
            lifecycle,
            runtime,
            new TorrentStatus(TorrentPhase.WaitingForEngine, false, 0, 0, 0),
            HasMetadata: true,
            SelectedFiles: [],
            PerTorrentSettings: null,
            DeferredActions: []);
    }

    private sealed class InMemoryCollectionRepository : ITorrentCollectionRepository
    {
        private readonly Dictionary<TorrentId, TorrentEntry> _entries;
        public int SaveCalls { get; private set; }

        public InMemoryCollectionRepository(IReadOnlyList<TorrentEntry> entries)
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

        public Task SaveAsync(bool force = true, CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ControlledGateway(bool startResult, bool pauseResult, bool removeResult) : ITorrentEngineGateway
    {
        public int StartCalls { get; private set; }

        public Task<bool> StartAsync(TorrentId id, CancellationToken ct = default)
        {
            StartCalls++;
            return Task.FromResult(startResult);
        }

        public Task<bool> PauseAsync(TorrentId id, CancellationToken ct = default)
            => Task.FromResult(pauseResult);

        public Task StopAsync(TorrentId id, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
            => Task.FromResult(removeResult);
    }

    private sealed class FixedRuntimeFactsProvider(IReadOnlyList<TorrentRuntimeFact> runtimeFacts) : ITorrentRuntimeFactsProvider
    {
        public event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;
        public IReadOnlyList<TorrentRuntimeFact> GetAll() => runtimeFacts;

        public void RaiseUpdated() => RuntimeFactsUpdated?.Invoke(runtimeFacts);
    }

    private sealed class TrackingEngineLifecycle : ITorrentEngineLifecycle
    {
        public int InitializeCalls { get; private set; }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopLegacyMigrationSource : ILegacyTorrentEntrySettingsMigrationSource
    {
        public TorrentEntrySettings Load(TorrentId torrentId) => TorrentEntrySettings.Default;
        public void Remove(TorrentId torrentId) { }
    }

    private sealed class InMemorySourceStore : ITorrentSourceStore
    {
        public Task SaveAsync(TorrentId id, TorrentKey key, byte[] torrentBytes, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<byte[]?> TryLoadAsync(TorrentId id, TorrentKey key, CancellationToken ct = default)
            => Task.FromResult<byte[]?>([1, 2, 3]);
    }

    private sealed class NoopWriteService : ITorrentWriteService
    {
        public Task<TorrentRuntimeState> AddAsync(TorrentId id, AddTorrentRequest request, CancellationToken ct = default)
            => Task.FromResult(TorrentRuntimeState.WaitingForEngine(0, false));

        public Task<TorrentRuntimeState> RehydrateAsync(TorrentEntry entry, byte[] torrentBytes, CancellationToken ct = default)
            => Task.FromResult(TorrentStatusResolver.ResolveExpectedRuntime(entry));

        public Task ApplySettingsAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}

