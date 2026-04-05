using CommunityToolkit.Mvvm.ComponentModel;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentItemViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;
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

    public TorrentItemViewModel(TorrentListItemReadModel item)
    {
        _item = item;
        Apply(item);
    }

    public bool Update(TorrentListItemReadModel item)
    {
        if (_isDisposed)
            return false;

        if (EqualityComparer<TorrentListItemReadModel>.Default.Equals(_item, item))
            return false;

        Apply(item);
        return true;
    }

    private static string BuildStateText(TorrentStatus status)
        => TorrentStatusTextMapper.ToUserFacingText(status);

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
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}
