using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Presentation.Abstractions;
using static NiTorrent.Application.Torrents.TorrentSource;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentViewModel(
    ITorrentItemViewModelFactory itemViewModelFactory,
    DeleteTorrentUseCase deleteTorrentUseCase,
    PreviewTorrentContentsUseCase previewTorrentContentsUseCase,
    CreateTorrentDownloadUseCase createTorrentDownloadUseCase,
    GetTorrentListQuery getTorrentListQuery,
    IDialogService dialogs,
    IPickerHelper pickerHelper,
    ITorrentRuntimeStateSource store,
    IUiDispatcher dispatcher,
    ITorrentPreviewService torrentPreview,
    RestoreSessionUseCase restoreSessionUseCase) : ObservableObject
{
    private readonly PreviewTorrentContentsUseCase _previewTorrentContentsUseCase = previewTorrentContentsUseCase;
    private readonly CreateTorrentDownloadUseCase _createTorrentDownloadUseCase = createTorrentDownloadUseCase;
    private readonly RestoreSessionUseCase _restoreSessionUseCase = restoreSessionUseCase;
    private readonly ITorrentItemViewModelFactory _itemViewModelFactory = itemViewModelFactory;
    private readonly DeleteTorrentUseCase _deleteTorrentUseCase = deleteTorrentUseCase;
    private readonly GetTorrentListQuery _getTorrentListQuery = getTorrentListQuery;
    private readonly IPickerHelper _pickerHelper = pickerHelper;
    private readonly ITorrentPreviewService _torrentPreview = torrentPreview;
    private readonly IDialogService _dialogs = dialogs;
    private readonly IUiDispatcher _dispatcher = dispatcher;
    private readonly ITorrentRuntimeStateSource _store = store;

    private readonly Dictionary<Guid, TorrentItemViewModel> _torrents = new();
    public ObservableCollection<TorrentItemViewModel> Torrents { get; set; } = new();
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
    private async Task OnRuntimeStateChanged(TorrentRuntimeStateChangedEventArgs e)
    {
        await _dispatcher.EnqueueAsync(() =>
        {
            foreach (var removed in e.RemovedIds)
            {
                if (_torrents.TryGetValue(removed, out var toRemove))
                {
                    _torrents.Remove(removed);
                    toRemove.Dispose();
                    Torrents.Remove(toRemove);
                    if (ReferenceEquals(SelectedTorrent, toRemove))
                        SelectedTorrent = null;
                }
            }
            var byId = e.UpdatedStatuses.ToDictionary(x => x.TorrentId);

            foreach (var item in Torrents)
            {
                if (byId.TryGetValue(item.Id, out var status))
                {
                    item.UpdateRuntime(status);
                }
            }
        });
    }

    public async Task TorrentLoading(CancellationToken ct)
    {
        var torrents = await _getTorrentListQuery.ExecuteAsync(ct);

        foreach (var torrent in torrents)
        {
            var torrentViewModel = _itemViewModelFactory.Create(torrent, RemoveTorrentAsync);
            Torrents.Add(torrentViewModel);
            _torrents.Add(torrentViewModel.Id, torrentViewModel);
        }
        _ = _restoreSessionUseCase.ExecuteAsync(ct);
        _store.Subscribe(OnRuntimeStateChanged);
    }
    public void TorrentUnloaded()
    {
        _store.UnsubscribeAsync(OnRuntimeStateChanged).GetAwaiter().GetResult();
        Torrents.Clear();
        _torrents.Clear();
    }

    partial void OnSelectedTorrentChanged(TorrentItemViewModel? value)
    {
        RefreshCommands();
    }

    public void RefreshCommands()
    {
    }

    [RelayCommand]
    private async Task PickTorrent(CancellationToken ct)
    {
        try
        {
            var path = await _pickerHelper.PickSingleFilePathAsync(".torrent");
            if (path is null)
                return;

            await PreviewTorrent(new TorrentFile(path), ct);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось добавить торрент";
            await _dialogs.ShowTextAsync("Ошибка добавления", UserErrorMapper.ToMessage(ex, "Не удалось добавить торрент."));
        }
    }

    private async Task PreviewTorrent(TorrentSource torrentSource, CancellationToken ct)
    {
        var previewTorrent = await _previewTorrentContentsUseCase.ExecuteAsync(new PreviewTorrentContentsCommand() { Source = torrentSource }, ct);

        var previewDialogResult = await _torrentPreview.ShowAsync(previewTorrent, ct);

        if (previewDialogResult is null)
            return;
        var command = new StartTorrentDownloadCommand(torrentSource, previewDialogResult.OutputFolder, previewDialogResult.SelectedFiles.ToList());
        var startedTorrent = await _createTorrentDownloadUseCase.ExecuteAsync(command, ct);

        var torrent = _itemViewModelFactory.Create(startedTorrent.TorrentDownload, RemoveTorrentAsync);

        _torrents.Add(torrent.Id, torrent);
        Torrents.Add(torrent);
    }

    public Task AddMagnet(string magnet, CancellationToken ct)
    {
        try
        {
            return PreviewTorrent(new Magnet(magnet), ct);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось добавить magnet-ссылку";
            return _dialogs.ShowTextAsync("Ошибка добавления", UserErrorMapper.ToMessage(ex, "Не удалось добавить торрент."));
        }
    }

    private async Task RemoveTorrentAsync(TorrentItemViewModel torrent, bool deleteData)
    {
        var toRemove = torrent;

        try
        {
            await _deleteTorrentUseCase.ExecuteAsync(new DeleteTorrentCommand(toRemove.Id, deleteData), CancellationToken.None);

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
