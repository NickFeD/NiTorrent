using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.Enum;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using Microsoft.Extensions.Logging;
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
    ITorrentSession session,
    ILogger<TorrentViewModel>? logger = null) : ObservableObject
{
    private readonly PreviewTorrentContentsUseCase _previewTorrentContentsUseCase = previewTorrentContentsUseCase;
    private readonly CreateTorrentDownloadUseCase _createTorrentDownloadUseCase = createTorrentDownloadUseCase;
    private readonly ITorrentSession _session = session;
    private readonly ITorrentItemViewModelFactory _itemViewModelFactory = itemViewModelFactory;
    private readonly DeleteTorrentUseCase _deleteTorrentUseCase = deleteTorrentUseCase;
    private readonly GetTorrentListQuery _getTorrentListQuery = getTorrentListQuery;
    private readonly IPickerHelper _pickerHelper = pickerHelper;
    private readonly ITorrentPreviewService _torrentPreview = torrentPreview;
    private readonly IDialogService _dialogs = dialogs;
    private readonly IUiDispatcher _dispatcher = dispatcher;
    private readonly ITorrentRuntimeStateSource _store = store;
    private readonly SemaphoreSlim _startupGate = new(1, 1);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private readonly Dictionary<Guid, TorrentItemViewModel> _torrents = new();
    private bool _isStoreSubscribed;
    private int _loadGeneration;
    public ObservableCollection<TorrentItemViewModel> Torrents { get; set; } = new();
    public bool IsEmpty => Torrents.Count < 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTorrent))]
    [NotifyPropertyChangedFor(nameof(IsDetailsEmpty))]
    public partial TorrentItemViewModel? SelectedTorrent { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Загрузка кэша торрентов...";

    [ObservableProperty]
    public partial string TotalDownloadSpeedDisplay { get; set; } = "v 0 B/s";

    [ObservableProperty]
    public partial string TotalUploadSpeedDisplay { get; set; } = "^ 0 B/s";

    [ObservableProperty]
    public partial string TotalSpeedDisplay { get; set; } = "v 0 B/s";

    public string TotalSpeedSubtitleDisplay => $"{TotalDownloadSpeedDisplay} • {TotalUploadSpeedDisplay}";

    public bool CanRemove => SelectedTorrent != null;
    public bool HasSelectedTorrent => SelectedTorrent != null;
    public bool IsDetailsEmpty => SelectedTorrent == null;
    public int TotalTorrentCount => Torrents.Count;
    public int ActiveTorrentCount => Torrents.Count(x => x.State.State is
        TorrentLifecycleState.Checking or
        TorrentLifecycleState.Moving or
        TorrentLifecycleState.FetchingMetadata or
        TorrentLifecycleState.Stalled or
        TorrentLifecycleState.Downloading or
        TorrentLifecycleState.Seeding);
    public int PausedTorrentCount => Torrents.Count(x => x.State.State is TorrentLifecycleState.Paused or TorrentLifecycleState.Stopped);
    public int CompletedTorrentCount => Torrents.Count(x => x.IsCompleted || x.State.State == TorrentLifecycleState.Seeding);
    public string TotalTorrentCountText => $"{TotalTorrentCount} всего";

    private void OnEngineStateChanged()
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (_isStoreSubscribed)
                StatusText = _session.EngineError ?? (_session.IsEngineReady ? "Движок готов" : "Подготовка движка");
        });
    }

    private async Task OnRuntimeStateChanged(TorrentRuntimeStateChangedEventArgs e)
    {
        await _refreshGate.WaitAsync();
        try
        {
            if (!_isStoreSubscribed) return;
            var generation = _loadGeneration;
            var collection = await _getTorrentListQuery.ExecuteAsync(CancellationToken.None);
            await _dispatcher.EnqueueAsync(() =>
            {
                if (!_isStoreSubscribed || generation != _loadGeneration) return;
                SynchronizeCollection(collection);
                UpdateTotalSpeeds();
                NotifyTorrentSummaryChanged();
                if (_session.EngineError is { } error) StatusText = error;
            });
        }
        finally { _refreshGate.Release(); }
    }

    private void SynchronizeCollection(IEnumerable<NiTorrent.Domain.Torrents.TorrentDownload> collection)
    {
        var records = collection.ToDictionary(x => x.Id);
        foreach (var id in _torrents.Keys.Where(id => !records.ContainsKey(id)).ToArray())
        {
            var item = _torrents[id];
            _torrents.Remove(id);
            Torrents.Remove(item);
            item.Dispose();
            if (ReferenceEquals(SelectedTorrent, item)) SelectedTorrent = null;
        }
        var snapshot = _store.GetSnapshot();
        foreach (var record in records.Values)
        {
            if (!_torrents.TryGetValue(record.Id, out var item))
            {
                item = _itemViewModelFactory.Create(record, RemoveTorrentAsync);
                _torrents.Add(record.Id, item);
                Torrents.Add(item);
            }
            item.ApplyDownload(record);
            if (snapshot.TryGetValue(record.Id, out var status)) item.UpdateRuntime(status);
        }
    }

    public async Task TorrentLoading(CancellationToken ct)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        await _startupGate.WaitAsync(ct);
        try
        {
            var generation = ++_loadGeneration;
            if (!_isStoreSubscribed)
            {
                _isStoreSubscribed = true;
                _store.Subscribe(OnRuntimeStateChanged);
                _session.EngineStateChanged += OnEngineStateChanged;
            }
            var torrents = await _getTorrentListQuery.ExecuteAsync(ct);
            await _dispatcher.EnqueueAsync(() =>
            {
                if (!_isStoreSubscribed || generation != _loadGeneration) return;
                SynchronizeCollection(torrents);
                UpdateTotalSpeeds();
                NotifyTorrentSummaryChanged();
                StatusText = _session.EngineError ?? "Коллекция загружена";
                logger?.LogInformation("[Lifecycle] Window collection projected: {Count} torrents in {ElapsedMs} ms",
                    Torrents.Count, elapsed.ElapsedMilliseconds);
            }, ct);
        }
        finally
        {
            _startupGate.Release();
        }
    }
    public void TorrentUnloaded()
    {
        ++_loadGeneration;
        if (_isStoreSubscribed)
        {
            _session.EngineStateChanged -= OnEngineStateChanged;
            _store.UnsubscribeAsync(OnRuntimeStateChanged).GetAwaiter().GetResult();
            _isStoreSubscribed = false;
        }

        foreach (var item in Torrents) item.Dispose();
        Torrents.Clear();
        _torrents.Clear();
        UpdateTotalSpeeds();
        NotifyTorrentSummaryChanged();
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

        if (_torrents.TryAdd(torrent.Id, torrent)) Torrents.Add(torrent);
        else torrent.Dispose();
        UpdateTotalSpeeds();
        NotifyTorrentSummaryChanged();
    }

    public async Task AddTorrentFileAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            await PreviewTorrent(new TorrentFile(path), ct);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось добавить торрент";
            await _dialogs.ShowTextAsync("Ошибка добавления", UserErrorMapper.ToMessage(ex, "Не удалось добавить торрент."), ct);
        }
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
            var records = await _getTorrentListQuery.ExecuteAsync(CancellationToken.None);
            await _dispatcher.EnqueueAsync(() =>
            {
                if (!_isStoreSubscribed) return;
                SynchronizeCollection(records);
                UpdateTotalSpeeds();
                NotifyTorrentSummaryChanged();
            });
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось удалить торрент";
            await _dialogs.ShowTextAsync("Ошибка удаления", UserErrorMapper.ToMessage(ex, "Не удалось удалить торрент."));
        }
    }

    private void UpdateTotalSpeeds()
    {
        var totalDownloadSpeed = Torrents.Sum(x => x.State.DownloadSpeed);
        var totalUploadSpeed = Torrents.Sum(x => x.State.UploadSpeed);
        var totalSpeed = totalDownloadSpeed + totalUploadSpeed;

        TotalDownloadSpeedDisplay = $"v {SizeFormatter.FormatSpeed(totalDownloadSpeed)}";
        TotalUploadSpeedDisplay = $"^ {SizeFormatter.FormatSpeed(totalUploadSpeed)}";
        TotalSpeedDisplay = $"v {SizeFormatter.FormatSpeed(totalSpeed)}";

        OnPropertyChanged(nameof(TotalSpeedSubtitleDisplay));
    }

    private void NotifyTorrentSummaryChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalTorrentCount));
        OnPropertyChanged(nameof(ActiveTorrentCount));
        OnPropertyChanged(nameof(PausedTorrentCount));
        OnPropertyChanged(nameof(CompletedTorrentCount));
        OnPropertyChanged(nameof(TotalTorrentCountText));
    }
}
