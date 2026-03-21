using System.Collections.ObjectModel;
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
    private readonly ITorrentWorkflowService _torrentWorkflowService;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;

    public ObservableCollection<TorrentItemViewModel> Torrents { get; set; } = new();

    private Dictionary<TorrentId,TorrentItemViewModel> _torrents = new();
    public bool IsEmpty => Torrents.Count < 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    public partial TorrentItemViewModel? SelectedTorrent { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Загрузка кэша торрентов...";

    [ObservableProperty]
    public partial string TotalDownloadSpeed { get; set; } = "v 0 KB/s";

    [ObservableProperty]
    public partial string TotalUploadSpeed { get; set; } = "^ 0 KB/s";

    public bool CanRemove => SelectedTorrent != null;

    public TorrentViewModel(
        ITorrentService torrentService,
        IDialogService dialogs,
        IUiDispatcher ui,
        ITorrentWorkflowService torrentWorkflowService)
    {
        _ui = ui;
        _torrentService = torrentService;
        _dialogs = dialogs;
        _torrentWorkflowService = torrentWorkflowService;
        _torrentService.UpdateTorrent += UpdateTorrent;
        _torrentService.Loaded += TorrentServiceLoaded;
    }

    private void TorrentServiceLoaded()
    {
        _ui.TryEnqueue(() =>
        {
            StatusText = "Движок торрентов готов";
        });
    }

    private void UpdateTorrent(IReadOnlyList<Domain.Torrents.TorrentSnapshot> torrents)
    {
        _ui.TryEnqueue(() =>
        {
            long totalDownloadSpeed = 0;
            long totalUploadSpeed = 0;
            var actualIds = torrents.Select(x => x.Id).ToHashSet();

            foreach (var stale in _torrents.Keys.Where(id => !actualIds.Contains(id)).ToList())
            {
                var staleVm = _torrents[stale];
                staleVm.Dispose();
                _torrents.Remove(stale);
                Torrents.Remove(staleVm);

                if (ReferenceEquals(SelectedTorrent, staleVm))
                    SelectedTorrent = null;
            }

            foreach (var torrent in torrents)
            {
                totalDownloadSpeed += torrent.Status.DownloadRateBytesPerSecond;
                totalUploadSpeed += torrent.Status.UploadRateBytesPerSecond;
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
            TotalDownloadSpeed = SizeFormatter.FormatSpeed(totalDownloadSpeed);
            TotalUploadSpeed = SizeFormatter.FormatSpeed(totalUploadSpeed);
            OnPropertyChanged(nameof(IsEmpty));
            if (SelectedTorrent != null)
                RefreshCommands();
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
        try
        {
            await _torrentWorkflowService.PickAndAddAsync();
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось добавить торрент";
            await _dialogs.ShowTextAsync("Ошибка добавления", ex.Message);
        }
    }

    public async Task AddMagnet(string magnet)
    {
        try
        {
            await _torrentWorkflowService.AddMagnetAsync(magnet);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось добавить magnet-ссылку";
            await _dialogs.ShowTextAsync("Ошибка добавления", ex.Message);
        }
    }

    // ---------------- COMMAND LOGIC ----------------

    private bool CanStart()
        => SelectedTorrent != null && (SelectedTorrent.State.Phase is TorrentPhase.Stopped or TorrentPhase.Paused or TorrentPhase.Error);

    private bool CanPause()
        => SelectedTorrent != null && (SelectedTorrent.State.Phase is TorrentPhase.WaitingForEngine or TorrentPhase.FetchingMetadata or TorrentPhase.Checking or TorrentPhase.Downloading or TorrentPhase.Seeding);

    private bool CanOpenFolder()
        => SelectedTorrent != null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (SelectedTorrent == null) return;

        try
        {
            StatusText = "Запуск торрента...";
            await _torrentWorkflowService.StartAsync(SelectedTorrent.Id);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось запустить торрент";
            await _dialogs.ShowTextAsync("Ошибка запуска", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        if (SelectedTorrent == null) return;

        try
        {
            StatusText = "Пауза торрента...";
            await _torrentWorkflowService.PauseAsync(SelectedTorrent.Id);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось поставить торрент на паузу";
            await _dialogs.ShowTextAsync("Ошибка паузы", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private Task OpenFolderAsync()
    {
        if (SelectedTorrent == null) return Task.CompletedTask;
        return _torrentWorkflowService.OpenFolderAsync(SelectedTorrent.SavePath);
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task RemoveTorrent(string isDeleteData)
    {
        if (SelectedTorrent == null) return;

        var toRemove = SelectedTorrent;

        try
        {
            await _torrentWorkflowService.RemoveAsync(SelectedTorrent.Id, isDeleteData == "1");

            _torrents.Remove(toRemove.Id);
            toRemove.Dispose();
            Torrents.Remove(toRemove);
            OnPropertyChanged(nameof(IsEmpty));

            if (ReferenceEquals(SelectedTorrent, toRemove))
                SelectedTorrent = null;
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось удалить торрент";
            await _dialogs.ShowTextAsync("Ошибка удаления", ex.Message);
        }
    }

}
