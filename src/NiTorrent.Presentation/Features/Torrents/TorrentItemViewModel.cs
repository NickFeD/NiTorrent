using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.Enum;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;
using TorrentLifecycleState = NiTorrent.Application.Torrents.Enum.TorrentLifecycleState;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentItemViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;
    private readonly Func<TorrentItemViewModel,bool, Task> _removeAsync;
    private readonly StartTorrentUseCase _startTorrentUseCase;
    private readonly PauseTorrentUseCase _pauseTorrentUseCase;
    private readonly IFolderLauncher _folderLauncher;
    private readonly IDialogService _dialogs;
    private readonly TorrentDownload _item;

    public Guid Id => _item.Id;

    public string Size => SizeFormatter.FormatBytes(_item.Size);
    public string Name => _item.Name;
    public string SavePath => _item.SavePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
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
        State = new TorrentRuntimeStatus(Id, Application.Torrents.Enum.TorrentLifecycleState.Unknown, null,0,0);
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
        => State.State is TorrentLifecycleState.Stopped or TorrentLifecycleState.Paused or TorrentLifecycleState.Error;

    private bool CanPause()
        => State.State is TorrentLifecycleState.FetchingMetadata or TorrentLifecycleState.Checking or TorrentLifecycleState.Downloading or TorrentLifecycleState.Seeding;

    private bool CanOpenFolder()
        => !string.IsNullOrWhiteSpace(SavePath);

    private static bool CanRemove()
        => true;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await _startTorrentUseCase.ExecuteAsync(new StartTorrentCommand(Id), ct);
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
            await _pauseTorrentUseCase.ExecuteAsync(new PauseTorrentCommand(Id), ct);
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
