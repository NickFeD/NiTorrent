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

    [ObservableProperty]
    public partial ObservableCollection<TorrentItemViewModel> Torrents { get; set; } = new();
    private Dictionary<TorrentId,TorrentItemViewModel> _torrents = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    public partial TorrentItemViewModel? SelectedTorrent { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "������";

    [ObservableProperty]
    public partial string TotalDownloadSpeed { get; set; } = "v 0 KB/s";

    [ObservableProperty]
    public partial string TotalUploadSpeed { get; set; } = "^ 0 KB/s";

    public bool CanRemove => SelectedTorrent != null;

    public TorrentViewModel(
        ITorrentPreviewDialogService previewDialog,
        ITorrentService torrentService,
        IPickerHelper pickerHelper)
    {
        _torrentService = torrentService;
        _pickerHelper = pickerHelper;
        _previewDialog = previewDialog;
        _torrentService.UptateTorrent += UptateTorrent;
    }

    private void UptateTorrent(IReadOnlyList<Domain.Torrents.TorrentSnapshot> torrents)
    {
        foreach (var torrent in torrents)
        {
            if (_torrents.TryGetValue(torrent.Id, out var oldTorrent))
            {
                oldTorrent.Update(torrent);
            }
            else
            {
                 var newTorrent = new TorrentItemViewModel(torrent);
                _torrents.Add(newTorrent.Id, newTorrent);
                Torrents.Add(newTorrent);
            }
        }
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
        await _previewDialog.ShowAsync(torrentPreview);
    }

    public async Task AddMagnet(string magnet)
    {
        var torrent = new Magnet(magnet);
        var torrentPreview = await _torrentService.GetPreviewAsync(torrent);
        await _previewDialog.ShowAsync(torrentPreview);
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
    private async Task RemoveTorrent(bool isDeleteData)
    {
        if (SelectedTorrent == null) return;

        var toRemove = SelectedTorrent;

        await _torrentService.RemoveAsync(SelectedTorrent.Id,isDeleteData);

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
