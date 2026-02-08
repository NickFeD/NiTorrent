using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;
using static NiTorrent.Application.Torrents.TorrentSource;


namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentViewModel : ObservableObject
{
    private readonly ITorrentService _torrentService;
    private readonly IPickerHelper _pickerHelper;
    private readonly ITorrentPreviewDialogService _previewDialog;
    private readonly IUiDispatcher _ui;

    public ObservableCollection<TorrentItemViewModel> Torrents { get; set; } = new();

    private Dictionary<TorrentId,TorrentItemViewModel> _torrents = new();
    public bool IsEmpty => Torrents.Count() < 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    public partial TorrentItemViewModel? SelectedTorrent { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "LoadingSystem";

    [ObservableProperty]
    public partial string TotalDownloadSpeed { get; set; } = "v 0 KB/s";

    [ObservableProperty]
    public partial string TotalUploadSpeed { get; set; } = "^ 0 KB/s";

    public bool CanRemove => SelectedTorrent != null;

    public TorrentViewModel(
        ITorrentPreviewDialogService previewDialog,
        ITorrentService torrentService,
        IPickerHelper pickerHelper,
        IUiDispatcher ui)
    {
        _ui = ui;
        _torrentService = torrentService;
        _pickerHelper = pickerHelper;
        _previewDialog = previewDialog;
        _torrentService.UptateTorrent += UptateTorrent;
        _torrentService.Loaded += TorrentServiceLoaded;
    }

    private void TorrentServiceLoaded()
    {
        StatusText = "Ok";
    }

    private void UptateTorrent(IReadOnlyList<Domain.Torrents.TorrentSnapshot> torrents)
    {
        _ui.TryEnqueue(() =>
        {
            long totalDownloadSpeed = 0;
            long totalUploadSpeed = 0;
            foreach (var torrent in torrents)
            {
                totalDownloadSpeed += torrent.Status.DownloadRateBytesPerSecond;
                totalUploadSpeed += torrent.Status.UploadRateBytesPerSecond;
                if (_torrents.TryGetValue(torrent.Id, out var oldTorrent))
                {
                    oldTorrent.Update(torrent);
                    PauseCommand.NotifyCanExecuteChanged();
                    OpenFolderCommand.NotifyCanExecuteChanged();
                }
                else
                {
                    var newTorrent = new TorrentItemViewModel(torrent);
                    _torrents.Add(newTorrent.Id, newTorrent);
                    Torrents.Add(newTorrent);
                }
            }
            TotalDownloadSpeed = SizeFormatter.FormatSpeed(totalDownloadSpeed);
            TotalUploadSpeed = SizeFormatter.FormatSpeed(totalUploadSpeed);
            OnPropertyChanged(nameof(IsEmpty));
        });
    }

    partial void OnSelectedTorrentChanged(TorrentItemViewModel? value)
    {
        RefreshCommands();
    }

    public void RefreshCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        RemoveTorrentCommand.NotifyCanExecuteChanged();
    }

    // ---------------- ADD ----------------

    [RelayCommand]
    private async Task PickTorrent()
    {

        var path = await _pickerHelper.PickSingleFilePathAsync(".torrent");
        if (path == null)
            return;
        
        var torrent = new TorrentFile(path);
        var torrentPreview = await _torrentService.GetPreviewAsync(torrent);
        var torrentPreviewDialogResult = await _previewDialog.ShowAsync(torrentPreview);
        if (torrentPreviewDialogResult is null)
            return;
        await _torrentService.AddAsync(new(torrent,torrentPreviewDialogResult.OutputFolder,torrentPreviewDialogResult.SelectedFilePaths.ToHashSet()));
    }

    public async Task AddMagnet(string magnet)
    {
        var torrent = new Magnet(magnet);
        var torrentPreview = await _torrentService.GetPreviewAsync(torrent);
        await _previewDialog.ShowAsync(torrentPreview);
        var torrentPreviewDialogResult = await _previewDialog.ShowAsync(torrentPreview);
        if (torrentPreviewDialogResult is null)
            return;
        await _torrentService.AddAsync(new(torrent, torrentPreviewDialogResult.OutputFolder, torrentPreviewDialogResult.SelectedFilePaths.ToHashSet()));
    }

    // ---------------- COMMAND LOGIC ----------------

    private bool CanStart()
        => SelectedTorrent != null && (SelectedTorrent.State.Phase is TorrentPhase.Stopped or TorrentPhase.Paused);

    private bool CanPause()
        => SelectedTorrent != null && (SelectedTorrent.State.Phase is TorrentPhase.Downloading or TorrentPhase.Seeding);

    private bool CanOpenFolder()
        => SelectedTorrent != null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (SelectedTorrent == null) return;
        StatusText = "������...";
        await _torrentService.StartAsync(SelectedTorrent.Id);
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        if (SelectedTorrent == null) return;
        StatusText = "�����...";
        await _torrentService.PauseAsync(SelectedTorrent.Id);
    }

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private Task OpenFolderAsync()
    {
        if (SelectedTorrent == null) return Task.CompletedTask;
        OpenFolder(SelectedTorrent.SavePath);
        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task RemoveTorrent(string isDeleteData)
    {
        if (SelectedTorrent == null) return;

        var toRemove = SelectedTorrent;

        await _torrentService.RemoveAsync(SelectedTorrent.Id,isDeleteData == "1");

        toRemove.Dispose();
        Torrents.Remove(toRemove);

        if (ReferenceEquals(SelectedTorrent, toRemove))
            SelectedTorrent = null;
    }

    private static void OpenFolder(string path)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
