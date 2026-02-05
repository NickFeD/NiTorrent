using CommunityToolkit.Mvvm.ComponentModel;
using NiTorrent.Domain.Torrents;


namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentItemViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;
    public TorrentId Id => _torrentSnapshot.Id;

    private TorrentSnapshot _torrentSnapshot;
    public string Size => _torrentSnapshot.Size.ToString(); //SizeFormatter.FormatBytes(Manager.Torrent?.Size ?? 0);
    public string Name => _torrentSnapshot.Name;
    public string SavePath => _torrentSnapshot.SavePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial double Progress { get; set; }

    public string ProgressText => $"{Progress:F1}%";

    [ObservableProperty]
    public partial TorrentStatus State { get; set; } = new(TorrentPhase.Unknown,false,0,0,0);

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial string DownloadSpeed { get; set; } = "0 B";

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
        DownloadSpeed = _torrentSnapshot.Status.DownloadRateBytesPerSecond.ToString();
        //DownloadSpeed = SizeFormatter.FormatSpeed(Manager.Monitor.DownloadRate);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}

