using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Presentation.Features.Torrents.Tree;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentPreviewViewModel : ObservableObject
{
    public TorrentPreview Torrent { get; }
    public string Name => Torrent.Name;
    public string Size => FormatSize(Torrent.TotalSize);

    [ObservableProperty]
    public partial string OutputFolder { get; set; }

    public TorrentTreeModel Tree { get; }
    public ObservableCollection<TorrentTreeItemViewModel> RootItems { get; } = new();

    public TorrentPreviewViewModel(TorrentPreview torrentPreview, ITorrentPreferences prefs)
    {
        Torrent = torrentPreview;

        OutputFolder = string.IsNullOrWhiteSpace(prefs.DefaultDownloadPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            : prefs.DefaultDownloadPath;

        Tree = new TorrentTreeModel(torrentPreview.Files.Select(f => f.FullPath));

        foreach (var root in TorrentTreeItemViewModel.CreateRootItems(Tree))
            RootItems.Add(root);
    }



    private string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024d / 1024d:F1} MB";
        return $"{bytes / 1024d / 1024d / 1024d:F1} GB";
    }
    public List<string> GetSelectedFiles()
        => Tree.GetSelectedFiles();

}

