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
    public async Task SortingKeepsChildrenInTheirFolderAndLeavesOtherFoldersUnloaded()
    {
        var tree = new TorrentTreeModel([
            new TorrentFileEntry("folder/b.bin", 200, true),
            new TorrentFileEntry("folder/a.bin", 100, true),
            new TorrentFileEntry("other/c.bin", 300, true)
        ]);
        var folder = TorrentTreeItemViewModel.CreateRootItems(tree).Single(x => x.Name == "folder");
        await folder.EnsureChildrenLoadedAsync();
        var original = folder.Children.ToArray();
        folder.SortLoadedChildren(true, true);
        Assert.Equal(new[] { "b.bin", "a.bin" }, folder.Children.Select(x => x.Name));
        Assert.Same(original.Single(x => x.Name == "b.bin"), folder.Children[0]);
        Assert.Equal(2, folder.Children.Count);
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
    public async Task SortCommandsOrderEachExpandedLevelAndApplyToFoldersOpenedLater()
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
        vm.SetSort(true);
        Assert.Equal(new[] { "a.bin", "z.bin" }, folder.Children.Select(x => x.Name));
        Assert.Equal(new[] { "root-a.bin", "root-z.bin" }, vm.RootItems.Where(x => x.IsFile).Select(x => x.Name));
        Assert.True(later.Children.Single().IsPlaceholder);
        vm.SetSort(true);
        Assert.Equal(new[] { "z.bin", "a.bin" }, folder.Children.Select(x => x.Name));
        await later.EnsureChildrenLoadedAsync();
        Assert.Equal(new[] { "c.bin", "b.bin" }, later.Children.Select(x => x.Name));
        vm.SetSort(false);
        Assert.Equal(new[] { "a.bin", "z.bin" }, folder.Children.Select(x => x.Name));
        Assert.Equal(new[] { "b.bin", "c.bin" }, later.Children.Select(x => x.Name));
    }

    [Fact]
    public async Task SortingRaisesOneResetKeepsFoldersFirstAndPreservesExpandedNodes()
    {
        TorrentFileEntry[] files = [
            new("z/a.bin", 10, true), new("a/a.bin", 10, true),
            new("z.txt", 30, true), new("a.txt", 20, true)
        ];
        var vm = new TorrentPreviewViewModel(new TorrentPreview(false, "test", "hash", 70, files));
        var folder = vm.RootItems.Single(x => x.Name == "z");
        await folder.EnsureChildrenLoadedAsync();
        folder.IsExpanded = true;
        var changes = new List<NotifyCollectionChangedAction>();
        vm.RootItems.CollectionChanged += (_, e) => changes.Add(e.Action);
        vm.SetSort(false);
        Assert.Equal(new[] { "z", "a", "z.txt", "a.txt" }, vm.RootItems.Select(x => x.Name));
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, changes);
        Assert.Same(folder, vm.RootItems[0]);
        Assert.True(folder.IsExpanded);
        Assert.Equal("a.bin", folder.Children.Single().Name);
    }
}
