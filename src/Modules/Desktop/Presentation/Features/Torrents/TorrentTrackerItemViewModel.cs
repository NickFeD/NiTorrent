using CommunityToolkit.Mvvm.ComponentModel;

namespace NiTorrent.Presentation.Features.Torrents;

public sealed class TorrentTrackerItemViewModel : ObservableObject
{
    public string Key { get; }

    private string _uri = string.Empty;
    private string _status = "Unknown";
    private string _lastAnnounce = "—";
    private string _nextAnnounce = "—";
    private string _message = "—";

    public string Uri
    {
        get => _uri;
        private set => SetProperty(ref _uri, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string LastAnnounce
    {
        get => _lastAnnounce;
        private set => SetProperty(ref _lastAnnounce, value);
    }

    public string NextAnnounce
    {
        get => _nextAnnounce;
        private set => SetProperty(ref _nextAnnounce, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    //public TorrentTrackerItemViewModel(TorrentTrackerSnapshot snapshot)
    //{
    //    Key = snapshot.Key;
    //    Update(snapshot);
    //}

    //public void Update(TorrentTrackerSnapshot snapshot)
    //{
    //    Uri = snapshot.Uri;
    //    Status = snapshot.Status;
    //    LastAnnounce = FormatDuration(snapshot.LastAnnounceAgo);
    //    NextAnnounce = FormatDuration(snapshot.NextAnnounceIn);
    //    Message = !string.IsNullOrWhiteSpace(snapshot.Failure)
    //        ? snapshot.Failure!
    //        : !string.IsNullOrWhiteSpace(snapshot.Warning)
    //            ? snapshot.Warning!
    //            : "—";
    //}

    private static string FormatDuration(TimeSpan? value)
    {
        if (!value.HasValue)
            return "—";

        if (value.Value.TotalHours >= 1)
            return $"{(int)value.Value.TotalHours}h {value.Value.Minutes}m";

        if (value.Value.TotalMinutes >= 1)
            return $"{(int)value.Value.TotalMinutes}m {value.Value.Seconds}s";

        return $"{Math.Max(0, value.Value.Seconds)}s";
    }
}
