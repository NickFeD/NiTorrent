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
using NiTorrent.Torrents.Contracts;

namespace NiTorrent.Lifecycle.Tests;

public sealed class PresentationTests
{
    [Fact]
    public async Task TrayVisibilitySuspendsPagePreservesSelectionAndResumesCurrentIntent()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        await f.Host.StartAsync();
        var torrents = f.Host.Services.GetRequiredService<ITorrentsModule>();
        var interactions = new UnusedInteractions();
        var vm = new TorrentViewModel(new ItemFactory(interactions, torrents), torrents,
            interactions, interactions, f.Observations, new SerialDispatcher(), interactions);
        var visibility = new NiTorrent.Presentation.DesktopObservationLifetime();
        var transitions = new List<bool>();
        visibility.VisibilityChanged += visible =>
        {
            transitions.Add(visible);
        };
        using var observation = new TorrentPageObservation(vm, visibility);
        await observation.ActivateAsync();
        var selected = Assert.Single(vm.Torrents);
        vm.SelectedTorrent = selected;
        for (var cycle = 0; cycle < 3; cycle++)
        {
            visibility.SetVisible(false);
            visibility.SetVisible(false);
            await torrents.PauseAsync(item.Id);
            Assert.Same(selected, vm.SelectedTorrent);
            visibility.SetVisible(true);
            await SessionFixture.UntilAsync(() => selected.StartCommand.CanExecute(null));
            Assert.Same(selected, Assert.Single(vm.Torrents));
            Assert.True(selected.StartCommand.CanExecute(null));
            await torrents.StartAsync(item.Id);
            await SessionFixture.UntilAsync(() => selected.PauseCommand.CanExecute(null));
        }
        Assert.Equal(new[] { false, true, false, true, false, true }, transitions);
        vm.TorrentUnloaded();
    }

    [Fact]
    public async Task OpeningClosingAndReopeningViewModelOnlyObservesExistingSession()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync();
        await f.Host.StartAsync();
        await SessionFixture.UntilAsync(() => f.Runtime.Managers.GetValueOrDefault(item.Id));
        var ui = new SerialDispatcher();
        var interactions = new UnusedInteractions();
        var torrents = f.Host.Services.GetRequiredService<ITorrentsModule>();
        var factory = new ItemFactory(interactions, torrents);
        var vm = new TorrentViewModel(factory, torrents, interactions, interactions, f.Observations, ui, interactions);
        await vm.TorrentLoading(default);
        await SessionFixture.UntilAsync(() => vm.Torrents.Count == 1 && vm.Torrents[0].State.DownloadSpeed == 123);
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
    private sealed class ItemFactory(UnusedInteractions interactions, ITorrentsModule torrents) : ITorrentItemViewModelFactory
    {
        public TorrentItemViewModel Create(TorrentListItem item, Func<TorrentItemViewModel, bool, Task> remove) =>
            new(item, remove, interactions, interactions, torrents);
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
