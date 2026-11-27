using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Features.Torrents.Tree;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentPreviewViewModel : ObservableObject
{
    private readonly Func<CancellationToken, Task<TorrentPreview>>? _loadPreview;
    private bool _closed;
    public TorrentPreview? Torrent { get; private set; }
    public string Name => Torrent?.Name ?? "";
    public string Size => PreviewSizeFormatter.Format(Tree.Root.TotalSize);
    public string SelectionSummary => $"Всего: {PreviewSizeFormatter.Format(Tree.Root.TotalSize)} (выбрано: {PreviewSizeFormatter.Format(Tree.Root.SelectedSize)}, файлов: {Tree.Root.SelectedFileCount} из {Tree.Root.TotalFileCount})";

    [ObservableProperty]
    public partial string OutputFolder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsReady => Torrent is { AlreadyExists: false } && !_closed;
    public bool CanConfirm => IsReady && Tree.Root.SelectedFileCount > 0 && !string.IsNullOrWhiteSpace(OutputFolder);

    public TorrentTreeModel Tree { get; private set; } = new([]);
    public TreeItemCollection RootItems { get; } = [];

    private TorrentPreviewViewModel()
    {
        //HACK нужно сдеалть настройки более правдивыми и юзать их, а не просто юзать загрузки

        OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        // string.IsNullOrWhiteSpace(prefs.DefaultDownloadPath)
        // ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        // : prefs.DefaultDownloadPath;

    }

    public TorrentPreviewViewModel(TorrentPreview torrentPreview) : this()
        => ApplyPreview(torrentPreview);

    public TorrentPreviewViewModel(Func<CancellationToken, Task<TorrentPreview>> loadPreview) : this()
        => _loadPreview = loadPreview;

    partial void OnOutputFolderChanged(string value) => OnPropertyChanged(nameof(CanConfirm));

    private bool CanLoad() => !_closed && Torrent is null;

    [RelayCommand(CanExecute = nameof(CanLoad), IncludeCancelCommand = true)]
    private async Task Load(CancellationToken ct)
    {
        if (!CanLoad() || _loadPreview is null) return;
        ErrorMessage = null;
        try
        {
            var preview = await _loadPreview(ct);
            ct.ThrowIfCancellationRequested();
            if (!_closed) ApplyPreview(preview);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!_closed)
                ErrorMessage = UserErrorMapper.ToMessage(ex, "Не удалось получить список файлов. Проверьте источник и подключение к сети и повторите попытку.");
        }
    }

    private void ApplyPreview(TorrentPreview preview)
    {
        Torrent = preview;
        if (preview.AlreadyExists)
        {
            ErrorMessage = "Этот торрент уже добавлен в коллекцию.";
        }
        else
        {
            Tree = new TorrentTreeModel(preview.Files);
            Tree.SelectionChanged += OnSelectionChanged;
            foreach (var root in TorrentTreeItemViewModel.CreateRootItems(Tree))
                RootItems.Add(root);
        }
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(CanConfirm));
        LoadCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanConfirm));
    }

    public void Close()
    {
        _closed = true;
        LoadCommand.Cancel();
        Tree.SelectionChanged -= OnSelectionChanged;
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(CanConfirm));
        LoadCommand.NotifyCanExecuteChanged();
    }

    public List<TorrentFileEntry> GetFiles()
        => Tree.GetFiles();

}

