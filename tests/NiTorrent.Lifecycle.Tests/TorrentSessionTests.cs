using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Domain.Torrents;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class TorrentSessionTests
{
    [Fact]
    public async Task ConfirmedAddBeforeEngineStartupIsPersistedAndRestored()
    {
        await using var f = new SessionFixture();
        var item = new TorrentDownload(Guid.NewGuid(), "early-add", "Confirmed", f.Storage.Root) { Status = TorrentDownloadStatus.Running };
        await f.Session.AddAsync(item, new NiTorrent.Application.Torrents.TorrentSource.TorrentBytes([1, 2, 3]), default);
        Assert.NotNull(await f.Repository.GetByIdAsync(item.Id, default));
        Assert.Equal(0, f.Engine.Attempts);
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Runtime.Managers.GetValueOrDefault(item.Id));
        Assert.Single(f.Runtime.Calls, call => call == ("Add", item.Id));
        await f.StopAsync();
        var rejected = new TorrentDownload(Guid.NewGuid(), "rejected", "Closed", f.Storage.Root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Session.AddAsync(rejected,
            new NiTorrent.Application.Torrents.TorrentSource.TorrentBytes([4]), default));
        Assert.Null(await f.Repository.GetByIdAsync(rejected.Id, default));
    }

    [Fact]
    public async Task ShutdownDrainsAdmittedCommandBeforePersistenceResourcesAreReleased()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        var entered = SessionFixture.Signal();
        var release = SessionFixture.Signal();
        var blocked = new BlockingUpdateRepository(f.Repository, entered, release);
        using var session = new NiTorrent.Application.Torrents.TorrentSession(blocked, f.Runtime, f.Engine,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NiTorrent.Application.Torrents.TorrentSession>.Instance);
        var command = session.SetStateAsync(item.Id, TorrentDownloadStatus.Paused, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        session.RequestStop();
        var drain = session.DrainCommandsAsync(default);
        Assert.False(drain.IsCompleted);
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command);
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TorrentDownloadStatus.Running, (await f.Repository.GetByIdAsync(item.Id, default))!.Status);
    }

    private sealed class BlockingUpdateRepository(ITorrentRepository inner, TaskCompletionSource entered,
        TaskCompletionSource release) : ITorrentRepository
    {
        public Task AddAsync(TorrentDownload item, NiTorrent.Application.Torrents.TorrentSource source, CancellationToken ct) => inner.AddAsync(item, source, ct);
        public Task DeleteAsync(Guid id, CancellationToken ct) => inner.DeleteAsync(id, ct);
        public Task<bool> ExistsByInfoHash(string hash, CancellationToken ct) => inner.ExistsByInfoHash(hash, ct);
        public Task<List<TorrentDownload>> GetAllAsync(CancellationToken ct) => inner.GetAllAsync(ct);
        public Task<IReadOnlyList<NiTorrent.Application.Torrents.DTo.StoredTorrent>> GetAllForRestoreAsync(CancellationToken ct) => inner.GetAllForRestoreAsync(ct);
        public Task<NiTorrent.Application.Torrents.DTo.StoredTorrent?> GetForRestoreAsync(Guid id, CancellationToken ct) => inner.GetForRestoreAsync(id, ct);
        public Task<TorrentDownload?> GetByIdAsync(Guid id, CancellationToken ct) => inner.GetByIdAsync(id, ct);
        public Task LoadingAsync(CancellationToken ct) => inner.LoadingAsync(ct);
        public Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
        public async Task UpdateAsync(TorrentDownload item, CancellationToken ct)
        {
            entered.SetResult();
            await release.Task;
            ct.ThrowIfCancellationRequested();
            await inner.UpdateAsync(item, ct);
        }
    }

    [Fact]
    public async Task HeadlessHostRestoresWithoutObserversAndAllowsLateSubscription()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Runtime.Managers.GetValueOrDefault(item.Id));
        Assert.Empty(f.Statuses.GetSnapshot());
        var observer = f.Observations.Observe(new(NiTorrent.Torrents.Contracts.TorrentObservationKind.Collection), (_, _) => Task.CompletedTask);
        await SessionFixture.UntilAsync(() => f.Statuses.TryGet(item.Id, out var s) && s.DownloadSpeed == 123);
        Assert.Equal(123, f.Statuses.GetSnapshot()[item.Id].DownloadSpeed);
        observer.Dispose();
        await f.Session.SetStateAsync(item.Id, TorrentDownloadStatus.Paused, default);
        await SessionFixture.UntilAsync(() => f.Runtime.Calls.Any(c => c == ("Pause", item.Id)));
        Assert.False(f.Runtime.Managers[item.Id]);
        await f.StopAsync();
        Assert.True(f.Engine.Stopped);
        Assert.True(f.Marker.Clean);
        var reloaded = new NiTorrent.Infrastructure.Torrents.JsonTorrentRepository(f.Storage);
        Assert.Equal(TorrentDownloadStatus.Paused, (await reloaded.GetByIdAsync(item.Id, default))!.Status);
    }

    [Fact]
    public async Task PauseAcceptedDuringAddPreventsRestoredRunningIntentFromStarting()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        var entered = SessionFixture.Signal();
        var release = SessionFixture.Signal();
        f.Runtime.Adding = async (_, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
        await f.Host.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Session.SetStateAsync(item.Id, TorrentDownloadStatus.Paused, default);
        release.SetResult();
        await SessionFixture.UntilAsync(() => f.Session.GetPreparations()[item.Id].State == TorrentPreparationState.Ready);
        Assert.DoesNotContain(("Start", item.Id), f.Runtime.Calls);
        Assert.False(f.Runtime.Managers[item.Id]);
        Assert.Equal(TorrentDownloadStatus.Paused, (await f.Repository.GetByIdAsync(item.Id, default))!.Status);
    }

    [Fact]
    public async Task LatestOfSeveralEarlyCommandsIsAppliedOnceEngineIsReady()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync(TorrentDownloadStatus.Paused);
        await f.Session.SetStateAsync(item.Id, TorrentDownloadStatus.Running, default);
        await f.Session.SetStateAsync(item.Id, TorrentDownloadStatus.Paused, default);
        await f.Session.SetStateAsync(item.Id, TorrentDownloadStatus.Running, default);
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Session.GetPreparations()[item.Id].State == TorrentPreparationState.Ready);
        Assert.True(f.Runtime.Managers[item.Id]);
        Assert.Single(f.Runtime.Calls, c => c == ("Add", item.Id));
    }

    [Fact]
    public async Task FailedTorrentDoesNotBlockOthersAndRetryCleansPartialManager()
    {
        await using var f = new SessionFixture();
        var bad = await f.AddAsync();
        var good = await f.AddAsync();
        f.Runtime.FailSelection = id => id == bad.Id;
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Session.GetPreparations().TryGetValue(bad.Id, out var p) && p.State == TorrentPreparationState.Failed
            && f.Session.GetPreparations().TryGetValue(good.Id, out var q) && q.State == TorrentPreparationState.Ready);
        Assert.False(f.Runtime.Managers.ContainsKey(bad.Id));
        Assert.True(f.Runtime.Managers[good.Id]);
        Assert.NotNull(f.Session.GetPreparations()[bad.Id].Error);
        f.Runtime.FailSelection = null;
        f.Session.Retry(bad.Id);
        await SessionFixture.UntilAsync(() => f.Session.GetPreparations()[bad.Id].State == TorrentPreparationState.Ready);
        Assert.Equal(2, f.Runtime.Calls.Count(c => c == ("Add", bad.Id)));
        Assert.Single(f.Runtime.Calls, c => c == ("Add", good.Id));
        Assert.Contains(("Remove", bad.Id), f.Runtime.Calls);
    }

    [Fact]
    public async Task PriorityQueueServesNormalWorkAfterFourUserRequests()
    {
        await using var f = new SessionFixture();
        var normal = await f.AddAsync();
        f.Session.Schedule(normal.Id, false);
        var high = new List<Guid>();
        for (var i = 0; i < 6; i++) { var item = await f.AddAsync(); high.Add(item.Id); f.Session.Schedule(item.Id); }
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Runtime.Calls.Count(c => c.Operation == "Add") == 7);
        var order = f.Runtime.Calls.Where(c => c.Operation == "Add").Select(c => c.Id).ToArray();
        Assert.Equal(high.Take(4), order.Take(4));
        Assert.Equal(normal.Id, order[4]);
        Assert.Equal(high.Skip(4), order.Skip(5));
    }

    [Fact]
    public async Task DeleteWithoutFilesDoesNotRequireStartingEngine()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        await f.Session.DeleteAsync(item.Id, false, default);
        Assert.Null(await f.Repository.GetByIdAsync(item.Id, default));
        Assert.Empty(f.Runtime.Calls);
        Assert.Equal(0, f.Engine.Attempts);
    }

    [Fact]
    public async Task DeleteDuringAddDoesNotResurrectOrStartTorrent()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        var entered = SessionFixture.Signal();
        var release = SessionFixture.Signal();
        f.Runtime.Adding = async (_, ct) => { entered.SetResult(); await release.Task.WaitAsync(ct); };
        await f.Host.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Session.DeleteAsync(item.Id, false, default);
        release.SetResult();
        await SessionFixture.UntilAsync(() => f.Runtime.Calls.Contains(("Remove", item.Id)));
        await SessionFixture.UntilAsync(() => !f.Session.GetPreparations().ContainsKey(item.Id));
        Assert.Null(await f.Repository.GetByIdAsync(item.Id, default));
        Assert.DoesNotContain(("Start", item.Id), f.Runtime.Calls);
    }

    [Fact]
    public async Task PendingDeleteWithFilesSurvivesReloadAndNeverStartsTransmission()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        await f.Session.DeleteAsync(item.Id, true, default);
        var disk = new NiTorrent.Infrastructure.Torrents.JsonTorrentRepository(f.Storage);
        var pending = await disk.GetByIdAsync(item.Id, default);
        Assert.Equal(TorrentDownloadStatus.Deleted, pending!.Status);
        Assert.True(pending.DeleteFiles);
        using var session = new NiTorrent.Application.Torrents.TorrentSession(disk, f.Runtime, f.Engine,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NiTorrent.Application.Torrents.TorrentSession>.Instance);
        var worker = session.RunAsync(default);
        await SessionFixture.UntilAsync(() => f.Runtime.Calls.Contains(("DeleteFiles", item.Id)));
        session.RequestStop();
        await worker;
        Assert.DoesNotContain(("Start", item.Id), f.Runtime.Calls);
        Assert.Null(await disk.GetByIdAsync(item.Id, default));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task EngineStartupHasOneRetryAndKeepsIntentOnPermanentFailure(int failures, bool succeeds)
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        f.Engine.Failures = failures;
        await f.Host.StartAsync();
        if (succeeds)
        {
            await f.Session.WaitForEngineAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
            await SessionFixture.UntilAsync(() => f.Runtime.Managers.GetValueOrDefault(item.Id));
        }
        else
        {
            await Assert.ThrowsAsync<IOException>(() => f.Session.WaitForEngineAsync(default));
            await f.Session.SetStateAsync(item.Id, TorrentDownloadStatus.Paused, default);
            Assert.Equal(TorrentDownloadStatus.Paused, (await f.Repository.GetByIdAsync(item.Id, default))!.Status);
            Assert.NotNull(f.Session.EngineError);
            Assert.Empty(f.Runtime.Calls);
        }
        Assert.Equal(2, f.Engine.Attempts);
    }

    [Fact]
    public async Task ShutdownCancelsInFlightAddAndNeverBeginsNextTorrent()
    {
        await using var f = new SessionFixture();
        await f.AddAsync();
        await f.AddAsync();
        var entered = SessionFixture.Signal();
        f.Runtime.Adding = async (_, ct) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); };
        await f.Host.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(f.Runtime.Calls);
        Assert.Empty(f.Runtime.Managers);
        Assert.True(f.Marker.Clean);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Session.SetStateAsync(Guid.NewGuid(), TorrentDownloadStatus.Running, default));
    }

    [Fact]
    public async Task ShutdownCancelsEnginePreparationWithoutRetrying()
    {
        await using var f = new SessionFixture();
        var entered = SessionFixture.Signal();
        f.Engine.Starting = async ct => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); };
        await f.Host.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, f.Engine.Attempts);
        Assert.Empty(f.Runtime.Calls);
    }

    [Fact]
    public async Task ShutdownCancelsAndDrainsMetadataOperations()
    {
        await using var f = new SessionFixture();
        await f.Host.StartAsync();
        var entered = SessionFixture.Signal();
        var operation = f.Session.UseEngineAsync(async ct =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return 1;
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.True(f.Engine.Stopped);
    }

    [Fact]
    public async Task EngineFailureIsPublishedWithoutAnyTorrentsOrStatusSubscribers()
    {
        await using var f = new SessionFixture();
        f.Engine.Failures = 2;
        var changed = SessionFixture.Signal();
        f.Session.EngineStateChanged += () => changed.TrySetResult();
        await f.Host.StartAsync();
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(f.Session.EngineError);
        Assert.False(f.Session.IsEngineReady);
        Assert.Equal(2, f.Engine.Attempts);
        Assert.Empty(await f.Repository.GetAllAsync(default));
    }

    [Fact]
    public async Task FailedEngineStopDoesNotWriteCleanShutdownMarker()
    {
        await using var f = new SessionFixture();
        await f.Host.StartAsync();
        await f.Session.WaitForEngineAsync(default);
        f.Engine.FailStop = true;
        await Assert.ThrowsAsync<IOException>(() => f.StopAsync());
        Assert.False(f.Marker.Clean);
    }
}
