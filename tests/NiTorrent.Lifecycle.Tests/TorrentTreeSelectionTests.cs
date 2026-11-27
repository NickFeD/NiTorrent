using NiTorrent.Domain.Torrents;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Presentation.Features.Torrents;
using NiTorrent.Presentation.Features.Torrents.Tree;
using Xunit;
using System.Collections.Specialized;

namespace NiTorrent.Lifecycle.Tests;

public class TorrentTreeSelectionTests
{
    [Fact]
    public async Task NestedFileSelectionUpdatesAncestorTotalsWithoutLoadingSibling()
    {
        var tree = new TorrentTreeModel([
            new TorrentFileEntry("folder/a.bin", 100, true),
            new TorrentFileEntry("folder/nested/b.bin", 200, true),
            new TorrentFileEntry("other/c.bin", 300, false)
        ]);
        var roots = TorrentTreeItemViewModel.CreateRootItems(tree);
        var folder = roots.Single(x => x.Name == "folder");
        var other = roots.Single(x => x.Name == "other");
        Assert.True(other.Children.Single().IsPlaceholder);
        Assert.Equal(300, tree.Root.SelectedSize);

        await folder.EnsureChildrenLoadedAsync();
        var nested = folder.Children.Single(x => x.Name == "nested");
        await nested.EnsureChildrenLoadedAsync();
        nested.Children.Single().ToggleCommand.Execute(false);

        Assert.Equal(100, tree.Root.SelectedSize);
        Assert.Equal(600, tree.Root.TotalSize);
        Assert.Equal(1, tree.Root.SelectedFileCount);
        Assert.Equal("100 B", folder.SizeText);
        Assert.True(other.Children.Single().IsPlaceholder);
    }

    [Fact]
    public void SelectingCollapsedFolderUpdatesTotalsForUnloadedDescendants()
    {
        var tree = new TorrentTreeModel([
            new TorrentFileEntry("folder/a.bin", 100, true),
            new TorrentFileEntry("folder/nested/b.bin", 200, true),
            new TorrentFileEntry("other.bin", 50, true)
        ]);
        var folder = TorrentTreeItemViewModel.CreateRootItems(tree).Single(x => x.Name == "folder");
        folder.ToggleCommand.Execute(false);

        Assert.Equal(50, tree.Root.SelectedSize);
        Assert.Equal(1, tree.Root.SelectedFileCount);
        Assert.Equal("0 B", folder.SizeText);
        Assert.True(folder.Children.Single().IsPlaceholder);
        Assert.All(tree.GetFiles().Where(x => x.FullPath.StartsWith("folder/")), x => Assert.False(x.IsSelected));
        folder.ToggleCommand.Execute(true);
        Assert.Equal(350, tree.Root.SelectedSize);
        Assert.Equal(3, tree.Root.SelectedFileCount);
    }

    [Fact]
    public void SelectedSizeUsesDecimalUnitsAndZeroForUnselectedFile()
    {
        var tree = new TorrentTreeModel([
            new TorrentFileEntry("large.iso", 1_002_000_000, true),
            new TorrentFileEntry("small.txt", 500, false)
        ]);
        var items = TorrentTreeItemViewModel.CreateRootItems(tree);
        Assert.Equal("1 GB", items.Single(x => x.Name == "large.iso").SizeText);
        Assert.Equal("0 B", items.Single(x => x.Name == "small.txt").SizeText);
    }

    [Fact]
    public async Task ChangingSelectionKeepsTreeOrderAndDoesNotReloadOtherFolders()
    {
        TorrentFileEntry[] files = [
            new("folder/z.bin", 500, true),
            new("folder/a.bin", 100, true),
            new("later/c.bin", 300, true),
            new("later/b.bin", 200, true),
            new("root-z.bin", 400, true),
            new("root-a.bin", 50, true)
        ];
        var vm = new TorrentPreviewViewModel(new TorrentPreview(false, "test", "hash", 1550, files));
        var folder = vm.RootItems.Single(x => x.Name == "folder");
        var later = vm.RootItems.Single(x => x.Name == "later");
        await folder.EnsureChildrenLoadedAsync();
        var before = vm.RootItems.ToArray();
        var childrenBefore = folder.Children.ToArray();
        var changes = new List<NotifyCollectionChangedAction>();
        vm.RootItems.CollectionChanged += (_, e) => changes.Add(e.Action);
        folder.Children.CollectionChanged += (_, e) => changes.Add(e.Action);
        folder.Children.Single(x => x.Name == "a.bin").ToggleCommand.Execute(false);
        Assert.Equal(before, vm.RootItems);
        Assert.Equal(childrenBefore, folder.Children);
        Assert.Empty(changes);
        Assert.True(later.Children.Single().IsPlaceholder);
        Assert.Equal(1450, vm.Tree.Root.SelectedSize);
    }
}
