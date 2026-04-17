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
    public string Size => FormatSize(Torrent.TotalSize);

    [ObservableProperty]
    public partial string OutputFolder { get; set; }

    public TorrentTreeModel Tree { get; }
    public ObservableCollection<TorrentTreeItemViewModel> RootItems { get; } = [];

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
    }



    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024d / 1024d:F1} MB";
        return $"{bytes / 1024d / 1024d / 1024d:F1} GB";
    }
    public List<TorrentFileEntry> GetFiles()
        => Tree.GetFiles();

}

