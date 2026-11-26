using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Enum;
using NiTorrent.Domain.Torrents;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class TrayBulkCommandTests
{
    [Fact]
    public async Task FailureOfOneTorrentDoesNotPreventOtherCommands()
    {
        await using var f = new SessionFixture();
        var failed = await f.AddAsync();
        var other = await f.AddAsync();
        var calls = new RecordingSession(failed.Id);
        var result = await new SetAllTorrentStatesUseCase(f.Repository, calls).ExecuteAsync(true);
        Assert.Equal(1, result.Applied);
        Assert.Equal(failed.Id, Assert.Single(result.Failed));
        Assert.Contains(other.Id, calls.Calls);
    }

    [Fact]
    public async Task CancellationDoesNotAdmitBulkCommands()
    {
        await using var f = new SessionFixture();
        await f.AddAsync();
        var calls = new RecordingSession(Guid.Empty);
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SetAllTorrentStatesUseCase(f.Repository, calls).ExecuteAsync(true, stop.Token));
        Assert.Empty(calls.Calls);
    }

    private sealed class RecordingSession(Guid failingId) : ITorrentSession
    {
        public List<Guid> Calls { get; } = [];
        public event Action? EngineStateChanged { add { } remove { } }
        public bool IsEngineReady => false;
        public string? EngineError => null;
        public Task SetStateAsync(Guid id, TorrentDownloadStatus state, CancellationToken ct)
        {
            Calls.Add(id);
            return id == failingId ? Task.FromException(new IOException("Persist failed")) : Task.CompletedTask;
        }
        public Task WaitForEngineAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<T> UseEngineAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(TorrentDownload item, NiTorrent.Application.Torrents.TorrentSource source, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, bool deleteFiles, CancellationToken ct) => throw new NotSupportedException();
        public void Schedule(Guid id, bool priority = true) => throw new NotSupportedException();
        public void Retry(Guid id) => throw new NotSupportedException();
        public IReadOnlyDictionary<Guid, TorrentPreparation> GetPreparations() => throw new NotSupportedException();
        public void RequestStop() => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(TorrentDownloadStatus.Created)]
    [InlineData(TorrentDownloadStatus.Failed)]
    [InlineData(TorrentDownloadStatus.Completed)]
    public async Task EveryLiveTorrentAcceptsBulkPauseAndResume(TorrentDownloadStatus initial)
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync(initial);
        var commands = new SetAllTorrentStatesUseCase(f.Repository, f.Session);
        var initiallyResumed = await commands.ExecuteAsync(true);
        Assert.Empty(initiallyResumed.Failed);
        Assert.Equal(TorrentDownloadStatus.Running, (await f.Repository.GetByIdAsync(item.Id, default))!.Status);
        var paused = await commands.ExecuteAsync(false);
        Assert.Empty(paused.Failed);
        Assert.Equal(TorrentDownloadStatus.Paused, (await f.Repository.GetByIdAsync(item.Id, default))!.Status);
        var resumed = await commands.ExecuteAsync(true);
        Assert.Empty(resumed.Failed);
        Assert.Equal(TorrentDownloadStatus.Running, (await f.Repository.GetByIdAsync(item.Id, default))!.Status);
    }

    [Fact]
    public async Task PauseAllDuringPreparationPersistsPauseWithoutCancellingPreparation()
    {
        await using var f = new SessionFixture();
        var first = await f.AddAsync();
        var second = await f.AddAsync();
        var entered = SessionFixture.Signal();
        var release = SessionFixture.Signal();
        f.Runtime.Adding = async (_, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
        await f.Host.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await new SetAllTorrentStatesUseCase(f.Repository, f.Session).ExecuteAsync(false);
        Assert.Equal(2, result.Applied);
        Assert.Empty(result.Failed);
        Assert.Equal(TorrentDownloadStatus.Paused, (await f.Repository.GetByIdAsync(first.Id, default))!.Status);
        Assert.Equal(TorrentDownloadStatus.Paused, (await f.Repository.GetByIdAsync(second.Id, default))!.Status);
        release.TrySetResult();
        await SessionFixture.UntilAsync(() => f.Runtime.Calls.Contains(("Pause", first.Id)) && f.Runtime.Calls.Contains(("Pause", second.Id)));
        Assert.DoesNotContain(f.Runtime.Calls, c => c.Operation == "Start");
        Assert.Equal(2, f.Runtime.Managers.Count);
    }

    [Fact]
    public async Task ResumeAllRestartsManualPausesAndRetriesFailedPreparation()
    {
        await using var f = new SessionFixture();
        var paused = await f.AddAsync(TorrentDownloadStatus.Paused);
        var failed = await f.AddAsync();
        var fail = true;
        f.Runtime.FailSelection = id => id == failed.Id && fail;
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Session.GetPreparations().TryGetValue(failed.Id, out var state) && state.State == TorrentPreparationState.Failed);
        fail = false;
        var result = await new SetAllTorrentStatesUseCase(f.Repository, f.Session).ExecuteAsync(true);
        Assert.Equal(2, result.Applied);
        Assert.Empty(result.Failed);
        await SessionFixture.UntilAsync(() => f.Runtime.Managers.GetValueOrDefault(paused.Id) && f.Runtime.Managers.GetValueOrDefault(failed.Id));
        Assert.Equal(TorrentDownloadStatus.Running, (await f.Repository.GetByIdAsync(paused.Id, default))!.Status);
        Assert.Equal(2, f.Runtime.Calls.Count(c => c == ("Add", failed.Id)));
        Assert.Contains(("Remove", failed.Id), f.Runtime.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BulkCommandsNeverResurrectPendingDeletion(bool running)
    {
        await using var f = new SessionFixture();
        var deleted = await f.AddAsync(TorrentDownloadStatus.Deleted);
        deleted.DeleteFiles = true;
        await f.Repository.UpdateAsync(deleted, default);
        var active = await f.AddAsync();
        var result = await new SetAllTorrentStatesUseCase(f.Repository, f.Session).ExecuteAsync(running);
        Assert.Equal(1, result.Applied);
        Assert.Empty(result.Failed);
        var stored = (await f.Repository.GetByIdAsync(deleted.Id, default))!;
        Assert.Equal(TorrentDownloadStatus.Deleted, stored.Status);
        Assert.True(stored.DeleteFiles);
        Assert.Equal(running ? TorrentDownloadStatus.Running : TorrentDownloadStatus.Paused,
            (await f.Repository.GetByIdAsync(active.Id, default))!.Status);
        Assert.DoesNotContain(deleted.Id, f.Session.GetPreparations().Keys);
    }
}
