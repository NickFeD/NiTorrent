using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Deferred;
using NiTorrent.Domain.Torrents;
using Xunit;

namespace NiTorrent.Application.Tests.Deferred;

public sealed class ReplayDeferredTorrentActionsWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_ConcurrentCalls_RunThroughSingleGate()
    {
        var entry = CreateEntryWithDeferred(DeferredActionType.Start, DateTimeOffset.UtcNow);
        var repository = new InMemoryCollectionRepository([entry]);
        var apply = new ConcurrencyTrackingApplyWorkflow();
        var logger = new TestLogger<ReplayDeferredTorrentActionsWorkflow>();
        var sut = new ReplayDeferredTorrentActionsWorkflow(repository, apply, logger);

        await Task.WhenAll(
            sut.ExecuteAsync(trigger: "t1"),
            sut.ExecuteAsync(trigger: "t2"));

        Assert.Equal(1, apply.MaxConcurrency);
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesDuplicateExecutionActions()
    {
        var id = TorrentId.New();
        var entry = CreateEntry(
            id,
            deferred: [
                new DeferredAction(DeferredActionType.Start, DateTimeOffset.UtcNow.AddSeconds(-5)),
                new DeferredAction(DeferredActionType.Start, DateTimeOffset.UtcNow)
            ]);

        var repository = new InMemoryCollectionRepository([entry]);
        var apply = new CapturingApplyWorkflow();
        var logger = new TestLogger<ReplayDeferredTorrentActionsWorkflow>();
        var sut = new ReplayDeferredTorrentActionsWorkflow(repository, apply, logger);

        await sut.ExecuteAsync(trigger: "normalize-test");

        var captured = Assert.Single(apply.CapturedEntries);
        Assert.Single(captured.DeferredActions);
        Assert.Equal(DeferredActionType.Start, captured.DeferredActions[0].Type);
    }

    [Fact]
    public async Task ExecuteAsync_LogsOutcomeForCycle()
    {
        var repository = new InMemoryCollectionRepository([CreateEntry(TorrentId.New(), deferred: [])]);
        var apply = new CapturingApplyWorkflow();
        var logger = new TestLogger<ReplayDeferredTorrentActionsWorkflow>();
        var sut = new ReplayDeferredTorrentActionsWorkflow(repository, apply, logger);

        await sut.ExecuteAsync(trigger: "log-test");

        Assert.Contains(logger.Messages, x => x.Contains("started", StringComparison.OrdinalIgnoreCase) && x.Contains("log-test", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, x => x.Contains("finished", StringComparison.OrdinalIgnoreCase) && x.Contains("log-test", StringComparison.OrdinalIgnoreCase));
    }

    private static TorrentEntry CreateEntryWithDeferred(DeferredActionType actionType, DateTimeOffset requestedAt)
        => CreateEntry(TorrentId.New(), [new DeferredAction(actionType, requestedAt)]);

    private static TorrentEntry CreateEntry(TorrentId id, IReadOnlyList<DeferredAction> deferred)
    {
        var runtime = new TorrentRuntimeState(TorrentLifecycleState.WaitingForEngine, false, 0, 0, 0, null, false);
        return new TorrentEntry(
            id,
            new TorrentKey($"k-{id.Value:N}"),
            "name",
            1,
            "C:\\downloads",
            DateTimeOffset.UtcNow,
            TorrentIntent.Running,
            TorrentLifecycleState.WaitingForEngine,
            runtime,
            new TorrentStatus(TorrentPhase.WaitingForEngine, false, 0, 0, 0),
            HasMetadata: true,
            SelectedFiles: Array.Empty<string>(),
            PerTorrentSettings: null,
            DeferredActions: deferred);
    }

    private sealed class ConcurrencyTrackingApplyWorkflow : IApplyDeferredTorrentActionsWorkflow
    {
        private int _current;
        public int MaxConcurrency { get; private set; }

        public async Task<ApplyDeferredTorrentActionsResult> ExecuteAsync(
            IReadOnlyList<TorrentEntry> entries,
            bool staggerStartupStarts = false,
            CancellationToken ct = default)
        {
            var concurrent = Interlocked.Increment(ref _current);
            if (concurrent > MaxConcurrency)
                MaxConcurrency = concurrent;

            await Task.Delay(50, ct);

            Interlocked.Decrement(ref _current);
            return new ApplyDeferredTorrentActionsResult(entries, [], [], []);
        }
    }

    private sealed class CapturingApplyWorkflow : IApplyDeferredTorrentActionsWorkflow
    {
        public List<TorrentEntry> CapturedEntries { get; } = [];

        public Task<ApplyDeferredTorrentActionsResult> ExecuteAsync(
            IReadOnlyList<TorrentEntry> entries,
            bool staggerStartupStarts = false,
            CancellationToken ct = default)
        {
            CapturedEntries.Clear();
            CapturedEntries.AddRange(entries);

            var updated = entries.Select(e => e.WithDeferredActions(Array.Empty<DeferredAction>())).ToList();
            return Task.FromResult(new ApplyDeferredTorrentActionsResult(updated, [], [], []));
        }
    }

    private sealed class InMemoryCollectionRepository : ITorrentCollectionRepository
    {
        private readonly Dictionary<TorrentId, TorrentEntry> _entries;

        public InMemoryCollectionRepository(IReadOnlyList<TorrentEntry> entries)
            => _entries = entries.ToDictionary(e => e.Id, e => e);

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

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
