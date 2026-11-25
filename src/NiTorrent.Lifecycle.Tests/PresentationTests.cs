using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Torrents;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class PresentationTests
{
    [Fact]
    public async Task OpeningClosingAndReopeningViewModelOnlyObservesExistingSession()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Statuses.TryGet(item.Id, out var s) && s.DownloadSpeed == 123);
        await SessionFixture.UntilAsync(() => f.Runtime.Managers.GetValueOrDefault(item.Id));
        var ui = new SerialDispatcher();
        var interactions = new UnusedInteractions();
        var start = new StartTorrentUseCase(f.Repository, f.Session);
        var pause = new PauseTorrentUseCase(f.Repository, f.Session);
        var factory = new ItemFactory(start, pause, interactions, f.Session);
        var metadata = f.Host.Services.GetRequiredService<ITorrentMetadataProvider>();
        var vm = new TorrentViewModel(factory, new DeleteTorrentUseCase(f.Repository, f.Session),
            new PreviewTorrentContentsUseCase(metadata, f.Repository),
            new CreateTorrentDownloadUseCase(f.Session, f.Host.Services.GetRequiredService<ITorrentDownloadFactory>(), f.Repository, metadata),
            new GetTorrentListQuery(f.Repository), interactions, interactions, f.Statuses, ui, interactions, f.Session);
        await vm.TorrentLoading(default);
        var first = Assert.Single(vm.Torrents);
        Assert.Equal(item.Id, first.Id);
        Assert.Equal(123, first.State.DownloadSpeed);
        await ui.EnqueueAsync(vm.TorrentUnloaded);
        Assert.Empty(vm.Torrents);
        await f.Session.SetStateAsync(item.Id, TorrentDownloadStatus.Paused, default);
        await SessionFixture.UntilAsync(() => f.Runtime.Calls.Contains(("Pause", item.Id)));
        await vm.TorrentLoading(default);
        Assert.True(Assert.Single(vm.Torrents).StartCommand.CanExecute(null));
        Assert.Single(f.Runtime.Calls, c => c == ("Add", item.Id));
        Assert.Equal(1, f.Engine.Attempts);
        await ui.EnqueueAsync(vm.TorrentUnloaded);
    }

    private sealed class SerialDispatcher : IUiDispatcher
    {
        private readonly object _gate = new();
        public bool TryEnqueue(Action action) { lock (_gate) action(); return true; }
        public Task EnqueueAsync(Action action, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate) action();
            return Task.CompletedTask;
        }
    }
    private sealed class ItemFactory(StartTorrentUseCase start, PauseTorrentUseCase pause,
        UnusedInteractions interactions, ITorrentSession session) : ITorrentItemViewModelFactory
    {
        public TorrentItemViewModel Create(TorrentDownload item, Func<TorrentItemViewModel, bool, Task> remove) =>
            new(item, remove, start, pause, interactions, interactions, session);
    }
    private sealed class UnusedInteractions : IDialogService, IPickerHelper, ITorrentPreviewService, IFolderLauncher
    {
        public Task ShowTextAsync(string title, string text, CancellationToken ct = default) => throw new InvalidOperationException("No dialog expected");
        public Task<WindowCloseChoice?> ShowWindowCloseChoiceAsync(bool value, CancellationToken ct = default) => throw new InvalidOperationException("No dialog expected");
        public Task<string?> PickSingleFilePathAsync(params string[] types) => throw new InvalidOperationException("No picker expected");
        public Task<string?> PickSingleFolderPathAsync(CancellationToken ct = default) => throw new InvalidOperationException("No picker expected");
        public Task<TorrentPreviewDialogResult?> ShowAsync(TorrentPreview preview, CancellationToken ct) => throw new InvalidOperationException("No preview expected");
        public Task OpenAsync(string path, CancellationToken ct = default) => throw new InvalidOperationException("No folder expected");
    }
}
