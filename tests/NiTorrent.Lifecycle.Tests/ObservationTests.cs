using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.Enum;
using NiTorrent.Infrastructure.Torrents;
using NiTorrent.Torrents.Contracts;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class ObservationTests
{
    [Fact]
    public async Task FirstObserverStartsCollectionAndLastLeaseStopsIt()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        Assert.Equal(0, f.Provider.CollectionCalls);
        var first = new Inbox();
        var second = new Inbox();
        var lease1 = f.Source.Observe(new(TorrentObservationKind.Collection), first.Receive);
        await first.Next(TorrentConnectionState.Connected);
        var lease2 = f.Source.Observe(new(TorrentObservationKind.Collection), second.Receive);
        await second.Next(TorrentConnectionState.Connected);
        lease1.Dispose();
        f.Provider.Speed = 456;
        var latest = await second.Next(TorrentConnectionState.Connected, s => s.DownloadSpeed == 456);
        Assert.Equal(456, latest.Statuses[f.Provider.Id].DownloadSpeed);
        lease2.Dispose();
        // Drain a collection admitted before the last disposal, then observe a full refresh interval.
        await SessionFixture.UntilAsync(() => f.Provider.Active == 0);
        var calls = f.Provider.CollectionCalls;
        await Task.Delay(1200);
        Assert.Equal(calls, f.Provider.CollectionCalls);
        Assert.Equal(1, f.Provider.MaximumActive);
    }

    [Fact]
    public async Task TrayOnlyUsesSummaryAndCollectionCanResumeWithCurrentData()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        var tray = new Inbox();
        using var trayLease = f.Source.Observe(new(TorrentObservationKind.Summary), tray.Receive);
        var totals = await tray.Next(TorrentConnectionState.Connected);
        Assert.Empty(totals.Statuses);
        Assert.Equal(123, totals.DownloadSpeed);
        Assert.Equal(0, f.Provider.CollectionCalls);
        var page = new Inbox();
        var pageLease = f.Source.Observe(new(TorrentObservationKind.Torrent, f.Provider.Id), page.Receive);
        Assert.Single((await page.Next(TorrentConnectionState.Connected, s => s.Statuses.Count == 1)).Statuses);
        pageLease.Dispose();
        f.Provider.Speed = 789;
        await tray.Next(TorrentConnectionState.Connected, s => s.DownloadSpeed == 789);
        var pageAgain = new Inbox();
        using var renewed = f.Source.Observe(new(TorrentObservationKind.Collection), pageAgain.Receive);
        Assert.Equal(789, (await pageAgain.Next(TorrentConnectionState.Connected,
            s => s.Statuses.Values.Any(v => v.DownloadSpeed == 789))).Statuses[f.Provider.Id].DownloadSpeed);
        Assert.True(f.Provider.SummaryCalls > 0);
    }

    [Fact]
    public async Task ConnectionFailureKeepsLastValuesAndRecoversAutomatically()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        var inbox = new Inbox();
        using var lease = f.Source.Observe(new(TorrentObservationKind.Collection), inbox.Receive);
        await inbox.Next(TorrentConnectionState.Connected);
        f.Provider.Fail = true;
        var stale = await inbox.Next(TorrentConnectionState.Disconnected);
        Assert.Equal(123, stale.DownloadSpeed);
        Assert.Equal(123, stale.Statuses[f.Provider.Id].DownloadSpeed);
        Assert.Equal("Связь потеряна", stale.Message);
        f.Provider.Speed = 654;
        f.Provider.Fail = false;
        var recovered = await inbox.Next(TorrentConnectionState.Connected, s => s.DownloadSpeed == 654);
        Assert.Null(recovered.Message);
        Assert.Equal(654, recovered.Statuses[f.Provider.Id].DownloadSpeed);
    }

    [Fact]
    public async Task SlowAndThrowingObserversDoNotBlockOtherConsumersOrAccumulateHistory()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowValues = new ConcurrentQueue<long>();
        using var slow = f.Source.Observe(new(TorrentObservationKind.Collection), async (snapshot, ct) =>
        {
            if (snapshot.Connection != TorrentConnectionState.Connected) return;
            slowValues.Enqueue(snapshot.DownloadSpeed);
            if (snapshot.DownloadSpeed == 123) { entered.TrySetResult(); await release.Task.WaitAsync(ct); }
        });
        using var broken = f.Source.Observe(new(TorrentObservationKind.Collection), (_, _) => throw new IOException("Observer fault"));
        var fast = new Inbox();
        using var healthy = f.Source.Observe(new(TorrentObservationKind.Collection), fast.Receive);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Provider.Speed = 200;
        await fast.Next(TorrentConnectionState.Connected, s => s.DownloadSpeed == 200);
        f.Provider.Speed = 300;
        await fast.Next(TorrentConnectionState.Connected, s => s.DownloadSpeed == 300);
        release.SetResult();
        await SessionFixture.UntilAsync(() => slowValues.Contains(300));
        Assert.DoesNotContain(200, slowValues);
        Assert.Equal(1, f.Provider.MaximumActive);
    }

    [Fact]
    public async Task UnsupportedDetailsDoNotStartCollectionAndHaveAnExplanation()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        var inbox = new Inbox();
        using var lease = f.Source.Observe(new(TorrentObservationKind.Peers, f.Provider.Id), inbox.Receive);
        var unsupported = await inbox.Next(TorrentConnectionState.Unsupported);
        Assert.False(string.IsNullOrWhiteSpace(unsupported.Message));
        Assert.Empty(unsupported.Statuses);
        Assert.Equal(0, f.Provider.CollectionCalls);
        Assert.Equal(0, f.Provider.SummaryCalls);
    }

    [Fact]
    public async Task TorrentScopesStayIndependentAndDisposedLeaseDoesNotReceiveNewValues()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        var first = new Inbox();
        var second = new Inbox();
        var lease1 = f.Source.Observe(new(TorrentObservationKind.Torrent, f.Provider.Id), first.Receive);
        using var lease2 = f.Source.Observe(new(TorrentObservationKind.Torrent, f.Provider.OtherId), second.Receive);
        Assert.Equal(f.Provider.Id, Assert.Single((await first.Next(TorrentConnectionState.Connected)).Statuses).Key);
        Assert.Equal(f.Provider.OtherId, Assert.Single((await second.Next(TorrentConnectionState.Connected)).Statuses).Key);
        lease1.Dispose();
        lease1.Dispose();
        f.Provider.Speed = 900;
        await second.Next(TorrentConnectionState.Connected, s => s.DownloadSpeed == 900);
        Assert.DoesNotContain(first.Values, s => s.DownloadSpeed == 900);
    }

    [Fact]
    public async Task ConcurrentLeasesShareOneCollectorAndShutdownCancelsDelivery()
    {
        await using var f = new Fixture();
        await f.StartAsync();
        var leases = new ConcurrentBag<IDisposable>();
        Parallel.For(0, 24, _ => leases.Add(f.Source.Observe(new(TorrentObservationKind.Collection), (_, _) => Task.CompletedTask)));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var blocking = f.Source.Observe(new(TorrentObservationKind.Collection), async (_, ct) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { cancelled.TrySetResult(); }
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await SessionFixture.UntilAsync(() => f.Provider.CollectionCalls > 0);
        Parallel.ForEach(leases, lease => lease.Dispose());
        await f.Source.StopAsync(default);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, f.Provider.MaximumActive);
        Assert.Throws<ObjectDisposedException>(() => f.Source.Observe(new(TorrentObservationKind.Collection), (_, _) => Task.CompletedTask));
    }

    [Theory]
    [InlineData(TorrentObservationKind.Torrent)]
    [InlineData(TorrentObservationKind.Peers)]
    [InlineData(TorrentObservationKind.Trackers)]
    [InlineData((TorrentObservationKind)999)]
    public void InvalidRequestIsRejectedBeforeStartingCollection(TorrentObservationKind kind)
    {
        using var source = new InMemoryTorrentRuntimeStateSource(new Provider(), NullLogger<InMemoryTorrentRuntimeStateSource>.Instance);
        Assert.ThrowsAny<ArgumentException>(() => source.Observe(new(kind), (_, _) => Task.CompletedTask));
    }

    private sealed class Inbox
    {
        private readonly Channel<TorrentObservationSnapshot> _updates = Channel.CreateUnbounded<TorrentObservationSnapshot>();
        public ConcurrentQueue<TorrentObservationSnapshot> Values { get; } = new();
        public Task Receive(TorrentObservationSnapshot snapshot, CancellationToken ct)
        {
            Values.Enqueue(snapshot); _updates.Writer.TryWrite(snapshot); return Task.CompletedTask;
        }
        public async Task<TorrentObservationSnapshot> Next(TorrentConnectionState state, Func<TorrentObservationSnapshot, bool>? predicate = null)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                var value = await _updates.Reader.ReadAsync(deadline.Token);
                if (value.Connection == state && (predicate?.Invoke(value) ?? true)) return value;
            }
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Provider Provider { get; } = new();
        public InMemoryTorrentRuntimeStateSource Source { get; }
        public Fixture() => Source = new(Provider, NullLogger<InMemoryTorrentRuntimeStateSource>.Instance);
        public Task StartAsync() => Source.StartAsync(default);
        public async ValueTask DisposeAsync() { await Source.StopAsync(default); Source.Dispose(); }
    }
    private sealed class Provider : ITorrentRuntimeStatusProvider, ITorrentSummaryProvider
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid OtherId { get; } = Guid.NewGuid();
        public volatile bool Fail;
        public volatile int Speed = 123;
        public int CollectionCalls, SummaryCalls, Active, MaximumActive;
        private readonly object _maximumGate = new();
        public async Task<IReadOnlyList<TorrentRuntimeStatus>> GetAllAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref CollectionCalls);
            var active = Interlocked.Increment(ref Active);
            lock (_maximumGate) MaximumActive = Math.Max(MaximumActive, active);
            try
            {
                await Task.Yield(); ct.ThrowIfCancellationRequested();
                if (Fail) throw new IOException("Connection failure");
                // Other torrent has no speed in aggregate but a distinct observable value.
                return [new(Id, TorrentLifecycleState.Downloading, null, Speed, 7, 42),
                    new(OtherId, TorrentLifecycleState.Paused, null, 0, 0, 1)];
            }
            finally { Interlocked.Decrement(ref Active); }
        }
        public Task<TorrentRuntimeStatus?> GetAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<TorrentTransferSummary> GetSummaryAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref SummaryCalls);
            if (Fail) throw new IOException("Connection failure");
            return Task.FromResult(new TorrentTransferSummary(Speed, 7));
        }
    }
}
