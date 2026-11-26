using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NiTorrent.Presentation.Features.Torrents.Tree;

/// <summary>
/// ViewModel-узел дерева для TreeView (ItemsSource).
/// Не содержит WinUI-типов (TreeViewNode и т.п.).
/// </summary>
public sealed partial class TorrentTreeItemViewModel : ObservableObject
{
    private readonly TorrentTreeModel _tree;
    private readonly FolderModel? _folder;
    private readonly FileNode? _file;

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _childrenLoaded;

    public TorrentTreeItemViewModel? Parent { get; }
    public TreeItemCollection Children { get; } = new();

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public string Name { get; }
    public bool IsFolder => _folder != null;
    public bool IsFile => _file != null;
    public string TypeGlyph => IsFolder ? "\uE8B7" : "\uE7C3";
    public long SelectedSize => _folder?.SelectedSize ?? (_file?.IsSelected == true ? _file.Size : 0);
    public string SizeText => IsPlaceholder ? "" : PreviewSizeFormatter.Format(SelectedSize);
    public double SizeColumnWidth => _tree.SizeColumnWidth;
    public Uri IconUri => new($"ms-appx:///Assets/FileTypes/{GetIconName()}.svg");

    private string GetIconName()
    {
        if (IsFolder) return "folder";
        if (IsPlaceholder) return "file";
        return Path.GetExtension(Name).ToLowerInvariant() switch
        {
            ".txt" or ".md" or ".rtf" or ".doc" or ".docx" or ".odt" or ".log" or ".nfo" => "document",
            ".pdf" => "pdf",
            ".xls" or ".xlsx" or ".ods" or ".csv" => "spreadsheet",
            ".ppt" or ".pptx" or ".odp" => "presentation",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" or ".zst" or ".cab" => "archive",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".svg" or ".tiff" or ".avif" => "image",
            ".mkv" or ".mp4" or ".avi" or ".mov" or ".webm" or ".m4v" or ".wmv" or ".mpg" or ".ts" => "video",
            ".mp3" or ".flac" or ".wav" or ".m4a" or ".aac" or ".ogg" or ".opus" or ".wma" or ".aiff" => "audio",
            ".iso" or ".img" or ".bin" or ".cue" or ".nrg" => "disc",
            ".cs" or ".cpp" or ".h" or ".js" or ".py" or ".json" or ".xml" or ".html" or ".css" or ".sh" or ".ps1" => "code",
            ".exe" or ".msi" or ".msix" or ".bat" or ".cmd" or ".apk" or ".appx" => "executable",
            ".ttf" or ".otf" or ".woff" or ".woff2" => "font",
            ".epub" or ".mobi" or ".azw" or ".azw3" or ".fb2" => "ebook",
            ".srt" or ".ass" or ".ssa" or ".vtt" or ".sub" => "subtitle",
            ".ini" or ".cfg" or ".conf" or ".toml" or ".yaml" or ".yml" => "settings",
            ".torrent" => "torrent",
            _ => "file"
        };
    }

    public bool IsPlaceholder { get; }

    public bool CanToggle => !IsPlaceholder;

    /// <summary>
    /// Tri-state чекбокс:
    /// - true  : выбран(о) всё
    /// - false : ничего не выбрано
    /// - null  : частично (indeterminate)
    /// </summary>
    public bool? IsChecked
    {
        get
        {
            if (IsPlaceholder)
                return null;

            if (_folder != null)
                return _folder.CheckState;

            return _file?.IsSelected;
        }
    }

    [RelayCommand]
    private void Toggle(bool? isChecked)
    {
        if (IsPlaceholder)
            return;

        if (_folder != null)
        {
            // В UI при IsThreeState=true чекбокс может попытаться выставить null.
            // Мы считаем null "вводом" (toggle) и переводим в bool.
            var current = _folder.CheckState;
            bool target = isChecked switch
            {
                true => true,
                false => false,
                null => current != true
            };

            if (current == target)
                return;

            _tree.SetFolderSelection(_folder, target);

            // пересчитать родителей (у потомков уже выставили CheckState в SetFolderSelection)
            _tree.UpdateSelectionUpwards(_folder.Parent);

            // обновить UI для уже загруженной части дерева
            RaiseCheckStateChangedLoadedSubtree();
            Parent?.RaiseCheckStateChangedUpwards();
            return;
        }

        if (_file != null)
        {
            bool target = isChecked == true;
            if (_file.IsSelected == target)
                return;

            _file.IsSelected = target;
            _tree.UpdateSelectionUpwards(_file.Parent);

            OnPropertyChanged(nameof(IsChecked));
            OnPropertyChanged(nameof(SizeText));
            Parent?.RaiseCheckStateChangedUpwards();
        }
    }
    private TorrentTreeItemViewModel(
        TorrentTreeModel tree,
        string name,
        TorrentTreeItemViewModel? parent,
        FolderModel? folder,
        FileNode? file,
        bool isPlaceholder)
    {
        _tree = tree;
        Name = name;
        Parent = parent;
        _folder = folder;
        _file = file;
        IsPlaceholder = isPlaceholder;
    }

    public static IReadOnlyList<TorrentTreeItemViewModel> CreateRootItems(TorrentTreeModel tree)
    {
        var roots = new List<TorrentTreeItemViewModel>();
        foreach (var folder in tree.Root.Folders.Values)
            roots.Add(CreateFolder(tree, folder, parent: null));
        foreach (var file in tree.Root.Files.Values)
            roots.Add(CreateFile(tree, file, parent: null));
        return roots;
    }

    private static TorrentTreeItemViewModel CreateFolder(TorrentTreeModel tree, FolderModel folder, TorrentTreeItemViewModel? parent)
    {
        var vm = new TorrentTreeItemViewModel(tree, folder.Name, parent, folder, file: null, isPlaceholder: false);

        // Чтобы TreeView показал "стрелочку" (expander) ДО подгрузки детей,
        // кладём плейсхолдер (он будет виден только после раскрытия).
        if (folder.HasChildren)
            vm.Children.Add(CreatePlaceholder(tree, vm));

        return vm;
    }

    private static TorrentTreeItemViewModel CreateFile(TorrentTreeModel tree, FileNode file, TorrentTreeItemViewModel? parent)
        => new(tree, file.Name, parent, folder: null, file: file, isPlaceholder: false);

    private static TorrentTreeItemViewModel CreatePlaceholder(TorrentTreeModel tree, TorrentTreeItemViewModel parent)
        => new(tree, "Загрузка...", parent, folder: null, file: null, isPlaceholder: true);

    /// <summary>
    /// Lazy-load: создаёт дочерние VM при первом раскрытии.
    /// Важно: метод не использует WinUI и безопасен к многократным вызовам.
    /// </summary>
    public async Task EnsureChildrenLoadedAsync(CancellationToken ct = default)
    {
        if (_folder is null || IsPlaceholder)
            return;

        if (_childrenLoaded)
            return;

        await _loadGate.WaitAsync(ct);
        try
        {
            if (_childrenLoaded)
                return;

            ct.ThrowIfCancellationRequested();

            _tree.SortIfNeeded(_folder);

            Children.Clear();

            foreach (var sub in _folder.Folders.Values)
                Children.Add(CreateFolder(_tree, sub, this));

            foreach (var f in _folder.Files.Values)
                Children.Add(CreateFile(_tree, f, this));

            _childrenLoaded = true;
            SortLoadedChildren(_tree.SortBySize, _tree.SortDescending);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public void SortLoadedChildren(bool bySize, bool descending)
    {
        if (!_childrenLoaded) return;
        SortItems(Children, bySize, descending);
        foreach (var child in Children)
            child.SortLoadedChildren(bySize, descending);
    }

    public void RefreshSizeColumnWidth()
    {
        OnPropertyChanged(nameof(SizeColumnWidth));
        if (!_childrenLoaded) return;
        foreach (var child in Children)
            child.RefreshSizeColumnWidth();
    }

    public static void SortItems(TreeItemCollection items, bool bySize, bool descending)
    {
        var comparer = StringComparer.CurrentCultureIgnoreCase;
        var sorted = bySize
            ? (descending
                ? items.OrderByDescending(x => x.IsFolder).ThenByDescending(x => x.SelectedSize).ThenBy(x => x.Name, comparer)
                : items.OrderByDescending(x => x.IsFolder).ThenBy(x => x.SelectedSize).ThenBy(x => x.Name, comparer))
            : (descending
                ? items.OrderByDescending(x => x.IsFolder).ThenByDescending(x => x.Name, comparer)
                : items.OrderByDescending(x => x.IsFolder).ThenBy(x => x.Name, comparer));
        items.ApplyOrder(sorted.ToArray());
    }

    private void RaiseCheckStateChangedLoadedSubtree()
    {
        OnPropertyChanged(nameof(IsChecked));
        OnPropertyChanged(nameof(SizeText));

        if (!_childrenLoaded)
            return;

        foreach (var child in Children)
        {
            if (child.IsPlaceholder)
                continue;

            child.RaiseCheckStateChangedLoadedSubtree();
        }
    }

    private void RaiseCheckStateChangedUpwards()
    {
        OnPropertyChanged(nameof(IsChecked));
        OnPropertyChanged(nameof(SizeText));
        Parent?.RaiseCheckStateChangedUpwards();
    }
}
