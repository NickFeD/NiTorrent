using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentItemViewModel : ObservableObject, IDisposable
{
    public readonly record struct UpdateResult(bool AnyChanged, bool CommandStateChanged);

    private bool _isDisposed;
    private readonly Func<TorrentItemViewModel, Task> _startAsync;
    private readonly Func<TorrentItemViewModel, Task> _pauseAsync;
    private readonly Func<TorrentItemViewModel, Task> _openFolderAsync;
    private readonly Func<TorrentItemViewModel, Task> _removeAsync;
    private readonly Func<TorrentItemViewModel, Task> _removeWithDataAsync;

    public TorrentId Id => _item.Id;

    private TorrentListItemReadModel _item;
    public string Size => SizeFormatter.FormatBytes(_item.Size);
    public string Name => _item.Name;
    public string SavePath => _item.SavePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial double Progress { get; set; }

    public string ProgressText => $"{Progress:F1}%";

    [ObservableProperty]
    public partial TorrentStatus State { get; set; } = new(TorrentPhase.Unknown, false, 0, 0, 0);

    [ObservableProperty]
    public partial string StateText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial string DownloadSpeed { get; set; } = "0 B";

    [ObservableProperty]
    public partial string UploadSpeed { get; set; } = "0 B";

    public TorrentItemViewModel(
        TorrentListItemReadModel item,
        Func<TorrentItemViewModel, Task> startAsync,
        Func<TorrentItemViewModel, Task> pauseAsync,
        Func<TorrentItemViewModel, Task> openFolderAsync,
        Func<TorrentItemViewModel, Task> removeAsync,
        Func<TorrentItemViewModel, Task> removeWithDataAsync)
    {
        _startAsync = startAsync;
        _pauseAsync = pauseAsync;
        _openFolderAsync = openFolderAsync;
        _removeAsync = removeAsync;
        _removeWithDataAsync = removeWithDataAsync;

        _item = item;
        Apply(item);
    }

    public UpdateResult Update(TorrentListItemReadModel item)
    {
        if (_isDisposed)
            return default;

        if (EqualityComparer<TorrentListItemReadModel>.Default.Equals(_item, item))
            return default;

        var previousPhase = State.Phase;
        Apply(item);
        return new UpdateResult(
            AnyChanged: true,
            CommandStateChanged: previousPhase != State.Phase);
    }

    private static string BuildStateText(TorrentStatus status)
        => TorrentStatusTextMapper.ToUserFacingText(status);

    private bool CanStart()
        => State.Phase is TorrentPhase.Stopped or TorrentPhase.Paused or TorrentPhase.Error;

    private bool CanPause()
        => State.Phase is TorrentPhase.WaitingForEngine or TorrentPhase.FetchingMetadata or TorrentPhase.Checking or TorrentPhase.Downloading or TorrentPhase.Seeding;

    private bool CanOpenFolder()
        => !string.IsNullOrWhiteSpace(SavePath);

    private bool CanRemove()
        => true;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync()
        => _startAsync(this);

    [RelayCommand(CanExecute = nameof(CanPause))]
    private Task PauseAsync()
        => _pauseAsync(this);

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private Task OpenFolderAsync()
        => _openFolderAsync(this);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveAsync()
        => _removeAsync(this);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveWithDataAsync()
        => _removeWithDataAsync(this);

    private void Apply(TorrentListItemReadModel item)
    {
        var previous = _item;
        _item = item;

        if (!string.Equals(previous.Name, item.Name, StringComparison.Ordinal))
            OnPropertyChanged(nameof(Name));

        if (previous.Size != item.Size)
            OnPropertyChanged(nameof(Size));

        if (!string.Equals(previous.SavePath, item.SavePath, StringComparison.Ordinal))
            OnPropertyChanged(nameof(SavePath));

        var previousPhase = State.Phase;
        if (!EqualityComparer<TorrentStatus>.Default.Equals(State, item.Status))
            State = item.Status;

        if (Progress != item.Status.Progress)
            Progress = item.Status.Progress;

        if (IsCompleted != item.Status.IsComplete)
            IsCompleted = item.Status.IsComplete;

        var downloadSpeed = SizeFormatter.FormatSpeed(item.Status.DownloadRateBytesPerSecond);
        if (!string.Equals(DownloadSpeed, downloadSpeed, StringComparison.Ordinal))
            DownloadSpeed = downloadSpeed;

        var uploadSpeed = SizeFormatter.FormatSpeed(item.Status.UploadRateBytesPerSecond);
        if (!string.Equals(UploadSpeed, uploadSpeed, StringComparison.Ordinal))
            UploadSpeed = uploadSpeed;

        var stateText = BuildStateText(item.Status);
        if (!string.Equals(StateText, stateText, StringComparison.Ordinal))
            StateText = stateText;

        if (previousPhase != State.Phase)
        {
            StartCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
        }

        OpenFolderCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        RemoveWithDataCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}
