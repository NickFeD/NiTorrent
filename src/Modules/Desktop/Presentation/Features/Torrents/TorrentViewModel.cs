using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Torrents.Contracts;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.Enum;
using NiTorrent.Presentation.Abstractions;
using Microsoft.Extensions.Logging;
using static NiTorrent.Application.Torrents.TorrentSource;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentViewModel(
    ITorrentItemViewModelFactory itemViewModelFactory,
    ITorrentsModule torrents,
    IDialogService dialogs,
    IPickerHelper pickerHelper,
    ITorrentObservations observations,
    IUiDispatcher dispatcher,
    ITorrentPreviewService torrentPreview,
    ILogger<TorrentViewModel>? logger = null) : ObservableObject
{
    private readonly ITorrentsModule _torrentsModule = torrents;
    private readonly ITorrentItemViewModelFactory _itemViewModelFactory = itemViewModelFactory;
    private readonly IPickerHelper _pickerHelper = pickerHelper;
    private readonly ITorrentPreviewService _torrentPreview = torrentPreview;
    private readonly IDialogService _dialogs = dialogs;
    private readonly IUiDispatcher _dispatcher = dispatcher;
    private readonly ITorrentObservations _observations = observations;
    private IDisposable? _observation;
    private IReadOnlyDictionary<Guid, TorrentRuntimeStatus> _snapshot = new Dictionary<Guid, TorrentRuntimeStatus>();
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
                StatusText = _torrentsModule.EngineError ?? (_torrentsModule.IsEngineReady ? "Движок готов" : "Подготовка движка");
        });
    }

    private async Task OnRuntimeStateChanged(TorrentObservationSnapshot snapshot, CancellationToken ct)
    {
        await _refreshGate.WaitAsync(ct);
        try
        {
            if (!_isStoreSubscribed) return;
            var generation = _loadGeneration;
            var collection = await _torrentsModule.GetAllAsync(ct);
            await _dispatcher.EnqueueAsync(() =>
            {
                if (!_isStoreSubscribed || generation != _loadGeneration) return;
                _snapshot = snapshot.Statuses;
                SynchronizeCollection(collection);
                UpdateTotalSpeeds();
                NotifyTorrentSummaryChanged();
                StatusText = (snapshot.Connection == TorrentConnectionState.Disconnected ? snapshot.Message : null)
                    ?? _torrentsModule.EngineError
                    ?? (_torrentsModule.IsEngineReady ? "Движок готов" : "Подготовка движка");
            }, ct);
        }
        finally { _refreshGate.Release(); }
    }

    private void SynchronizeCollection(IEnumerable<TorrentListItem> collection)
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
        var snapshot = _snapshot;
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
            ct.ThrowIfCancellationRequested();
            var generation = ++_loadGeneration;
            if (!_isStoreSubscribed)
            {
                _isStoreSubscribed = true;
                _observation = _observations.Observe(new(TorrentObservationKind.Collection), OnRuntimeStateChanged);
                _torrentsModule.EngineStateChanged += OnEngineStateChanged;
            }
            var torrents = await _torrentsModule.GetAllAsync(ct);
            await _dispatcher.EnqueueAsync(() =>
            {
                if (!_isStoreSubscribed || generation != _loadGeneration) return;
                SynchronizeCollection(torrents);
                UpdateTotalSpeeds();
                NotifyTorrentSummaryChanged();
                StatusText = _torrentsModule.EngineError ?? "Коллекция загружена";
                logger?.LogInformation("[Lifecycle] Window collection projected: {Count} torrents in {ElapsedMs} ms",
                    Torrents.Count, elapsed.ElapsedMilliseconds);
            }, ct);
        }
        finally
        {
            _startupGate.Release();
        }
    }
    public void SuspendObservation()
    {
        ++_loadGeneration;
        if (_isStoreSubscribed)
        {
            _torrentsModule.EngineStateChanged -= OnEngineStateChanged;
            _isStoreSubscribed = false;
            _observation?.Dispose();
            _observation = null;
        }

    }

    public void TorrentUnloaded()
    {
        SuspendObservation();
        foreach (var item in Torrents) item.Dispose();
        Torrents.Clear();
        _torrents.Clear();
        SelectedTorrent = null;
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
        var previewTorrent = await _torrentsModule.PreviewAsync(torrentSource, ct);

        var previewDialogResult = await _torrentPreview.ShowAsync(previewTorrent, ct);

        if (previewDialogResult is null)
            return;
        var startedTorrent = await _torrentsModule.AddAsync(torrentSource, previewDialogResult.OutputFolder, previewDialogResult.SelectedFiles, ct);

        var torrent = _itemViewModelFactory.Create(startedTorrent, RemoveTorrentAsync);

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
            await _torrentsModule.DeleteAsync(toRemove.Id, deleteData, CancellationToken.None);
            var records = await _torrentsModule.GetAllAsync(CancellationToken.None);
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
