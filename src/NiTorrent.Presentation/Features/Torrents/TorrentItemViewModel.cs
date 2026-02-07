using CommunityToolkit.Mvvm.ComponentModel;
using NiTorrent.Domain.Torrents;


namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentItemViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;
    public TorrentId Id => _torrentSnapshot.Id;

    private TorrentSnapshot _torrentSnapshot;
    public string Size => SizeFormatter.FormatBytes(_torrentSnapshot.Size);
    public string Name => _torrentSnapshot.Name;
    public string SavePath => _torrentSnapshot.SavePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial double Progress { get; set; }

    public string ProgressText => $"{Progress:F1}%";

    [ObservableProperty]
    public partial TorrentStatus State { get; set; } = new(TorrentPhase.Unknown,false,0,0,0);

    [ObservableProperty]
    public partial string StateText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial string DownloadSpeed { get; set; } = "0 B";

    [ObservableProperty]
    public partial string UploadSpeed { get; set; } = "0 B";
    public TorrentItemViewModel(TorrentSnapshot torrentSnapshot)
    {
        _torrentSnapshot = torrentSnapshot;
        Update(torrentSnapshot);
    }

    public void Update(TorrentSnapshot torrentSnapshot)
    {
        if (_isDisposed)
            return;

        _torrentSnapshot = torrentSnapshot;

        State = _torrentSnapshot.Status;
        Progress = _torrentSnapshot.Status.Progress;
        IsCompleted = _torrentSnapshot.Status.IsComplete;
        DownloadSpeed = SizeFormatter.FormatSpeed(_torrentSnapshot.Status.DownloadRateBytesPerSecond);
        UploadSpeed = SizeFormatter.FormatSpeed(_torrentSnapshot.Status.UploadRateBytesPerSecond);
        StateText = State.Phase.ToString();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}

