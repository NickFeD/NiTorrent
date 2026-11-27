using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Presentation.Features.Torrents;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class TorrentPreviewLoadingTests
{
    [Fact]
    public async Task LoadingCommandShowsWaitingThenPreservesFileSelectionAndFolder()
    {
        var pending = new TaskCompletionSource<TorrentPreview>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new TorrentPreviewViewModel(_ => pending.Task);
        vm.OutputFolder = "chosen-folder";
        var loading = vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.LoadCommand.IsRunning);
        Assert.False(vm.LoadCommand.CanExecute(null));
        Assert.False(vm.IsReady);
        Assert.False(vm.CanConfirm);
        Assert.Empty(vm.RootItems);

        pending.SetResult(Preview());
        await loading.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(vm.LoadCommand.IsRunning);
        Assert.True(vm.IsReady);
        Assert.True(vm.CanConfirm);
        Assert.Equal("sample", vm.Name);
        Assert.Equal("chosen-folder", vm.OutputFolder);
        Assert.Collection(vm.GetFiles(), file => Assert.True(file.IsSelected), file => Assert.False(file.IsSelected));
        vm.RootItems.Single(x => x.Name == "a.bin").ToggleCommand.Execute(false);
        Assert.False(vm.CanConfirm);
        vm.RootItems.Single(x => x.Name == "b.bin").ToggleCommand.Execute(true);
        Assert.True(vm.CanConfirm);
        vm.OutputFolder = " ";
        Assert.False(vm.CanConfirm);
    }

    [Fact]
    public async Task MetadataFailureStaysInWindowAndRetryLoadsPreview()
    {
        var attempts = 0;
        var vm = new TorrentPreviewViewModel(_ => ++attempts == 1
            ? Task.FromException<TorrentPreview>(new UserVisibleException("Источник недоступен"))
            : Task.FromResult(Preview()));

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.Equal("Источник недоступен", vm.ErrorMessage);
        Assert.False(vm.CanConfirm);
        Assert.True(vm.LoadCommand.CanExecute(null));

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, attempts);
        Assert.False(vm.HasError);
        Assert.True(vm.CanConfirm);
        Assert.Equal(2, vm.RootItems.Count);
    }

    [Fact]
    public async Task ClosingCancelsMetadataRequestWithoutShowingError()
    {
        CancellationToken received = default;
        var vm = new TorrentPreviewViewModel(async ct =>
        {
            received = ct;
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Preview();
        });
        var loading = vm.LoadCommand.ExecuteAsync(null);
        vm.Close();
        await loading.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(received.IsCancellationRequested);
        Assert.False(vm.LoadCommand.IsRunning);
        Assert.False(vm.HasError);
        Assert.False(vm.IsReady);
        Assert.False(vm.CanConfirm);
        Assert.False(vm.LoadCommand.CanExecute(null));
    }

    [Fact]
    public async Task MetadataArrivingAfterCloseCannotPopulatePreview()
    {
        var pending = new TaskCompletionSource<TorrentPreview>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new TorrentPreviewViewModel(_ => pending.Task);
        var loading = vm.LoadCommand.ExecuteAsync(null);
        vm.Close();
        pending.SetResult(Preview());
        await loading.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(vm.Torrent);
        Assert.Empty(vm.RootItems);
        Assert.False(vm.HasError);
        Assert.False(vm.CanConfirm);
    }

    [Fact]
    public async Task AlreadyAddedTorrentExplainsWhyConfirmationIsUnavailable()
    {
        var vm = new TorrentPreviewViewModel(_ => Task.FromResult(Preview() with { AlreadyExists = true }));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Этот торрент уже добавлен в коллекцию.", vm.ErrorMessage);
        Assert.True(vm.HasError);
        Assert.False(vm.IsReady);
        Assert.False(vm.CanConfirm);
        Assert.False(vm.LoadCommand.CanExecute(null));
    }

    private static TorrentPreview Preview() => new(false, "sample", "hash", 300,
        [new("a.bin", 100, true), new("b.bin", 200, false)]);
}
