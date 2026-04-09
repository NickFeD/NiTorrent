using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentViewModel : ObservableObject
{
    private readonly ITorrentReadModelFeed _readModelFeed;
    private readonly ITorrentEngineStatusService _engineStatusService;
    private readonly ITorrentWorkflowService _torrentWorkflowService;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;
    private readonly object _pendingUpdateSync = new();
    private IReadOnlyList<TorrentListItemReadModel> _pendingItems = Array.Empty<TorrentListItemReadModel>();
    private int _uiUpdateScheduled;
    private bool _isActive;

    public ObservableCollection<TorrentItemViewModel> Torrents { get; set; } = new();

    private readonly Dictionary<TorrentId, TorrentItemViewModel> _torrents = new();
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
        ITorrentReadModelFeed readModelFeed,
        ITorrentEngineStatusService engineStatusService,
        IDialogService dialogs,
        IUiDispatcher ui,
        ITorrentWorkflowService torrentWorkflowService)
    {
        _ui = ui;
        _readModelFeed = readModelFeed;
        _engineStatusService = engineStatusService;
        _dialogs = dialogs;
        _torrentWorkflowService = torrentWorkflowService;

        if (_engineStatusService.IsReady)
            StatusText = "Движок торрентов готов";
    }

    public void Activate()
    {
        if (_isActive)
            return;

        _isActive = true;
        _readModelFeed.Updated += UpdateTorrent;
        _engineStatusService.Ready += TorrentEngineReady;
        UpdateTorrent(_readModelFeed.Current);
    }

    public void Deactivate()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _readModelFeed.Updated -= UpdateTorrent;
        _engineStatusService.Ready -= TorrentEngineReady;

        lock (_pendingUpdateSync)
            _pendingItems = Array.Empty<TorrentListItemReadModel>();
    }

    private void TorrentEngineReady()
    {
        _ui.TryEnqueue(() => { StatusText = "Движок торрентов готов"; });
    }

    private void UpdateTorrent(IReadOnlyList<TorrentListItemReadModel> torrents)
    {
        if (!_isActive)
            return;

        lock (_pendingUpdateSync)
            _pendingItems = torrents;

        if (Interlocked.Exchange(ref _uiUpdateScheduled, 1) == 1)
            return;

        if (!_ui.TryEnqueue(DrainPendingUpdatesOnUiThread))
            Interlocked.Exchange(ref _uiUpdateScheduled, 0);
    }

    private void DrainPendingUpdatesOnUiThread()
    {
        if (!_isActive)
        {
            Interlocked.Exchange(ref _uiUpdateScheduled, 0);
            return;
        }

        IReadOnlyList<TorrentListItemReadModel> torrents;
        lock (_pendingUpdateSync)
            torrents = _pendingItems;

        var previousCount = Torrents.Count;
        var commandsNeedRefresh = false;

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
                var update = oldTorrent.Update(torrent);
                if (update.CommandStateChanged && ReferenceEquals(oldTorrent, SelectedTorrent))
                    commandsNeedRefresh = true;
            }
            else
            {
                var newTorrent = new TorrentItemViewModel(
                    torrent,
                    ExecuteStartAsync,
                    ExecutePauseAsync,
                    ExecuteOpenFolderAsync,
                    ExecuteRemoveAsync,
                    ExecuteRemoveWithDataAsync);
                _torrents.Add(newTorrent.Id, newTorrent);
                Torrents.Add(newTorrent);
            }
        }

        TotalDownloadSpeed = SizeFormatter.FormatSpeed(totalDownloadSpeed);
        TotalUploadSpeed = SizeFormatter.FormatSpeed(totalUploadSpeed);

        if (previousCount != Torrents.Count)
            OnPropertyChanged(nameof(IsEmpty));

        if (commandsNeedRefresh)
            RefreshCommands();

        Interlocked.Exchange(ref _uiUpdateScheduled, 0);

        lock (_pendingUpdateSync)
        {
            if (ReferenceEquals(torrents, _pendingItems))
                return;
        }

        if (Interlocked.Exchange(ref _uiUpdateScheduled, 1) == 0)
        {
            if (!_ui.TryEnqueue(DrainPendingUpdatesOnUiThread))
                Interlocked.Exchange(ref _uiUpdateScheduled, 0);
        }
    }

    partial void OnSelectedTorrentChanged(TorrentItemViewModel? value)
    {
        RefreshCommands();
    }

    public void RefreshCommands()
    {
    }

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
            await _dialogs.ShowTextAsync("Ошибка добавления", UserErrorMapper.ToMessage(ex, "Не удалось добавить торрент."));
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
            await _dialogs.ShowTextAsync("Ошибка добавления", UserErrorMapper.ToMessage(ex, "Не удалось добавить торрент."));
        }
    }

    private async Task ExecuteStartAsync(TorrentItemViewModel torrent)
    {
        try
        {
            StatusText = "Запуск торрента...";
            await _torrentWorkflowService.StartAsync(torrent.Id);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось запустить торрент";
            await _dialogs.ShowTextAsync("Ошибка запуска", UserErrorMapper.ToMessage(ex, "Не удалось запустить торрент."));
        }
    }

    private async Task ExecutePauseAsync(TorrentItemViewModel torrent)
    {
        try
        {
            StatusText = "Пауза торрента...";
            await _torrentWorkflowService.PauseAsync(torrent.Id);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось поставить торрент на паузу";
            await _dialogs.ShowTextAsync("Ошибка паузы", UserErrorMapper.ToMessage(ex, "Не удалось поставить торрент на паузу."));
        }
    }

    private async Task ExecuteOpenFolderAsync(TorrentItemViewModel torrent)
    {
        try
        {
            await _torrentWorkflowService.OpenFolderAsync(torrent.SavePath);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось открыть папку торрента";
            await _dialogs.ShowTextAsync("Ошибка открытия папки", UserErrorMapper.ToMessage(ex, "Не удалось открыть папку торрента."));
        }
    }

    private Task ExecuteRemoveAsync(TorrentItemViewModel torrent)
        => RemoveTorrentCoreAsync(torrent, deleteData: false);

    private Task ExecuteRemoveWithDataAsync(TorrentItemViewModel torrent)
        => RemoveTorrentCoreAsync(torrent, deleteData: true);

    private async Task RemoveTorrentCoreAsync(TorrentItemViewModel torrent, bool deleteData)
    {
        var toRemove = torrent;

        try
        {
            await _torrentWorkflowService.RemoveAsync(toRemove.Id, deleteData);

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
            await _dialogs.ShowTextAsync("Ошибка удаления", UserErrorMapper.ToMessage(ex, "Не удалось удалить торрент."));
        }
    }
}
