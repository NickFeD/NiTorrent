using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Features.Torrents.Tree;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentPreviewViewModel : ObservableObject
{
    public TorrentPreview Torrent { get; }
    public string Name => Torrent.Name;
    public string Size => PreviewSizeFormatter.Format(Tree.Root.TotalSize);
    public string SelectionSummary => $"Всего: {PreviewSizeFormatter.Format(Tree.Root.TotalSize)} (выбрано: {PreviewSizeFormatter.Format(Tree.Root.SelectedSize)}, файлов: {Tree.Root.SelectedFileCount} из {Tree.Root.TotalFileCount})";

    [ObservableProperty]
    public partial double SizeColumnWidth { get; set; } = 140;

    partial void OnSizeColumnWidthChanged(double value)
    {
        Tree.SizeColumnWidth = value;
        foreach (var item in RootItems)
            item.RefreshSizeColumnWidth();
    }

    [ObservableProperty]
    public partial string OutputFolder { get; set; }

    public TorrentTreeModel Tree { get; }
    public TreeItemCollection RootItems { get; } = [];

    public TorrentPreviewViewModel(TorrentPreview torrentPreview)
    {
        Torrent = torrentPreview;

        //HACK нужно сдеалть настройки более правдивыми и юзать их, а не просто юзать загрузки

        OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        // string.IsNullOrWhiteSpace(prefs.DefaultDownloadPath)
        // ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        // : prefs.DefaultDownloadPath;

        Tree = new TorrentTreeModel(torrentPreview.Files);

        foreach (var root in TorrentTreeItemViewModel.CreateRootItems(Tree))
            RootItems.Add(root);
        ApplySort();
        Tree.SelectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectionSummary));
            if (Tree.SortBySize) ApplySort();
        };
    }



    public void SetSort(bool bySize)
    {
        Tree.SortDescending = Tree.SortBySize == bySize && !Tree.SortDescending;
        Tree.SortBySize = bySize;
        ApplySort();
    }
    private void ApplySort()
    {
        TorrentTreeItemViewModel.SortItems(RootItems, Tree.SortBySize, Tree.SortDescending);
        foreach (var item in RootItems)
            item.SortLoadedChildren(Tree.SortBySize, Tree.SortDescending);
    }
    public List<TorrentFileEntry> GetFiles()
        => Tree.GetFiles();

}

