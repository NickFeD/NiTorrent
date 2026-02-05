using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NiTorrent.Presentation.Features.Torrents.Tree;

/// <summary>
/// ViewModel-узел дерева для TreeView (ItemsSource).
/// Не содержит WinUI-типов (TreeViewNode и т.п.).
/// </summary>
public sealed class TorrentTreeItemViewModel : ObservableObject
{
    private readonly TorrentTreeModel _tree;
    private readonly FolderModel? _folder;
    private readonly FileNode? _file;

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _childrenLoaded;

    public TorrentTreeItemViewModel? Parent { get; }
    public ObservableCollection<TorrentTreeItemViewModel> Children { get; } = new();

    public string Name { get; }
    public bool IsFolder => _folder != null;
    public bool IsFile => _file != null;
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
        set
        {
            if (IsPlaceholder)
                return;

            if (_folder != null)
            {
                // В UI при IsThreeState=true чекбокс может попытаться выставить null.
                // Мы считаем null "вводом" (toggle) и переводим в bool.
                var current = _folder.CheckState;
                bool target = value switch
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
                bool target = value == true;
                if (_file.IsSelected == target)
                    return;

                _file.IsSelected = target;
                _tree.UpdateSelectionUpwards(_file.Parent);

                OnPropertyChanged(nameof(IsChecked));
                Parent?.RaiseCheckStateChangedUpwards();
            }
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
        // На всякий случай: после построения дерева привести CheckState папок в консистентное состояние
        tree.UpdateSelectionFromChildren();

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
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private void RaiseCheckStateChangedLoadedSubtree()
    {
        OnPropertyChanged(nameof(IsChecked));

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
        Parent?.RaiseCheckStateChangedUpwards();
    }
}
