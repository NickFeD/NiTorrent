using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;
using TorrentLifecycleState = NiTorrent.Application.Torrents.Enum.TorrentLifecycleState;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentItemViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;
    private readonly Func<TorrentItemViewModel, bool, Task> _removeAsync;
    private readonly StartTorrentUseCase _startTorrentUseCase;
    private readonly PauseTorrentUseCase _pauseTorrentUseCase;
    private readonly IFolderLauncher _folderLauncher;
    private readonly IDialogService _dialogs;
    private TorrentDownload _item;

    public Guid Id => _item.Id;

    public string Size => SizeFormatter.FormatBytes(_item.Size);
    public string SizeDetailsText => $"{Size} ({_item.Size:N0} байт)";
    public string Name => _item.Name;
    public string SavePath => _item.SavePath;
    public string HashText => string.IsNullOrWhiteSpace(_item.InfoHash) ? "—" : _item.InfoHash;
    public string AddedAtText => "—";

    public long DownloadedBytes => Math.Clamp((long)Math.Round(_item.Size * Progress / 100d), 0, _item.Size);
    public long RemainingBytes => Math.Max(0, _item.Size - DownloadedBytes);
    public string DownloadedText => $"{SizeFormatter.FormatBytes(DownloadedBytes)} ({DownloadedBytes:N0} байт)";
    public string RemainingText => RemainingBytes <= 0 ? "—" : SizeFormatter.FormatBytes(RemainingBytes);
    public string ProgressSummaryText => $"{SizeFormatter.FormatBytes(DownloadedBytes)} из {Size}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(DownloadedBytes))]
    [NotifyPropertyChangedFor(nameof(RemainingBytes))]
    [NotifyPropertyChangedFor(nameof(DownloadedText))]
    [NotifyPropertyChangedFor(nameof(RemainingText))]
    [NotifyPropertyChangedFor(nameof(ProgressSummaryText))]
    public partial double Progress { get; set; }

    public string ProgressText => $"{Progress:F1}%";

    [ObservableProperty]
    public partial TorrentRuntimeStatus State { get; set; }

    [ObservableProperty]
    public partial string StateText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial string DownloadSpeed { get; set; } = "0 B";

    [ObservableProperty]
    public partial string UploadSpeed { get; set; } = "0 B";

    [ObservableProperty]
    public partial string PeersText { get; set; } = "—";

    [ObservableProperty]
    public partial string TrackersText { get; set; } = "—";

    [ObservableProperty]
    public partial string EtaText { get; set; } = "—";

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public TorrentItemViewModel(
        TorrentDownload item,
        Func<TorrentItemViewModel, bool, Task> removeAsync,
        StartTorrentUseCase startTorrentUseCase,
        PauseTorrentUseCase pauseTorrentUseCase,
        IDialogService dialogs,
        IFolderLauncher folderLauncher)
    {
        _removeAsync = removeAsync;
        _startTorrentUseCase = startTorrentUseCase;
        _pauseTorrentUseCase = pauseTorrentUseCase;
        _folderLauncher = folderLauncher;
        _dialogs = dialogs;
        _item = item;
        State = new TorrentRuntimeStatus(Id, Application.Torrents.Enum.TorrentLifecycleState.Unknown, null, 0, 0, 0);
    }

    private static string BuildStateText(TorrentRuntimeStatus status)
    {
        if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
            return status.ErrorMessage;

        return status.State switch
        {
            TorrentLifecycleState.Unknown => "Неизвестно",
            TorrentLifecycleState.Stopped => "Остановлен",
            TorrentLifecycleState.Paused => "На паузе",
            TorrentLifecycleState.FetchingMetadata => "Получение метаданных",
            TorrentLifecycleState.Checking => "Проверка",
            TorrentLifecycleState.Downloading => "Скачивание",
            TorrentLifecycleState.Seeding => "Раздача",
            //TorrentLifecycleState.Completed => "Завершён",
            TorrentLifecycleState.Error => "Ошибка",
            _ => "Неизвестно"
        };
    }

    private bool CanStart()
        => _item.Status is TorrentDownloadStatus.Paused or TorrentDownloadStatus.Failed;
    //=> State.State is TorrentLifecycleState.Stopped or TorrentLifecycleState.Paused or TorrentLifecycleState.Error;

    private bool CanPause()
        => _item.Status == TorrentDownloadStatus.Running;
    // => State.State is TorrentLifecycleState.FetchingMetadata or TorrentLifecycleState.Checking or TorrentLifecycleState.Downloading or TorrentLifecycleState.Seeding;

    private bool CanOpenFolder()
        => !string.IsNullOrWhiteSpace(SavePath);

    private static bool CanRemove()
        => true;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var test = await _startTorrentUseCase.ExecuteAsync(new StartTorrentCommand(Id), ct);
            _item.Status = test.Status;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowTextAsync("Ошибка запуска", UserErrorMapper.ToMessage(ex, "Не удалось запустить торрент."), ct);
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync(CancellationToken ct)
    {
        try
        {
            var test = await _pauseTorrentUseCase.ExecuteAsync(new PauseTorrentCommand(Id), ct);
            _item.Status = test.Status;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowTextAsync("Ошибка паузы", UserErrorMapper.ToMessage(ex, "Не удалось поставить торрент на паузу."), ct);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private Task OpenFolderAsync()
        => _folderLauncher.OpenAsync(SavePath);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveAsync()
        => _removeAsync(this, false);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveWithDataAsync()
        => _removeAsync(this, true);

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    [RelayCommand]
    private Task OpenSettingsAsync()
        => _dialogs.ShowTextAsync("Настройки торрента", "Настройки для выбранного торрента пока недоступны.");

    [RelayCommand]
    private Task ShowDetailsStubAsync()
        => _dialogs.ShowTextAsync("Подробности торрента", "Расширенная статистика будет подключена позже.");

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }

    public void UpdateRuntime(TorrentRuntimeStatus status)
    {
        if (status.TorrentId != Id)
            return;

        var previousLifecycleState = State.State;

        if (State != status)
            State = status;

        if (Progress != status.Progress)
            Progress = status.Progress;

        //var isCompleted = status.State == TorrentLifecycleState.Completed;
        //if (IsCompleted != isCompleted)
        //    IsCompleted = isCompleted;

        var formattedDownloadSpeed = SizeFormatter.FormatSpeed(status.DownloadSpeed);
        if (!string.Equals(DownloadSpeed, formattedDownloadSpeed, StringComparison.Ordinal))
            DownloadSpeed = formattedDownloadSpeed;

        var formattedUploadSpeed = SizeFormatter.FormatSpeed(status.UploadSpeed);
        if (!string.Equals(UploadSpeed, formattedUploadSpeed, StringComparison.Ordinal))
            UploadSpeed = formattedUploadSpeed;

        var stateText = BuildStateText(status);
        if (!string.Equals(StateText, stateText, StringComparison.Ordinal))
            StateText = stateText;

        if (previousLifecycleState != status.State)
        {
            StartCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
        }
    }
}
