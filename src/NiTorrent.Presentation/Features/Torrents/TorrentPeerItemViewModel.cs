using CommunityToolkit.Mvvm.ComponentModel;
using NiTorrent.Application.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

public sealed class TorrentPeerItemViewModel : ObservableObject
{
    public string Key { get; }

    private string _endpoint = string.Empty;
    private string _client = "Unknown";
    private string _progress = "—";
    private string _downloadSpeed = "0 B/s";
    private string _uploadSpeed = "0 B/s";
    private string _ratio = "—";
    private string _flags = "—";

    public string Endpoint
    {
        get => _endpoint;
        private set => SetProperty(ref _endpoint, value);
    }

    public string Client
    {
        get => _client;
        private set => SetProperty(ref _client, value);
    }

    public string Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public string DownloadSpeed
    {
        get => _downloadSpeed;
        private set => SetProperty(ref _downloadSpeed, value);
    }

    public string UploadSpeed
    {
        get => _uploadSpeed;
        private set => SetProperty(ref _uploadSpeed, value);
    }

    public string Ratio
    {
        get => _ratio;
        private set => SetProperty(ref _ratio, value);
    }

    public string Flags
    {
        get => _flags;
        private set => SetProperty(ref _flags, value);
    }

    public TorrentPeerItemViewModel(TorrentPeerSnapshot snapshot)
    {
        Key = snapshot.Key;
        Update(snapshot);
    }

    public void Update(TorrentPeerSnapshot snapshot)
    {
        Endpoint = string.IsNullOrWhiteSpace(snapshot.Endpoint) ? "<unknown>" : snapshot.Endpoint;
        Client = string.IsNullOrWhiteSpace(snapshot.Client) ? "Unknown" : snapshot.Client;
        Progress = snapshot.ProgressPercent.HasValue ? $"{snapshot.ProgressPercent.Value:F1}%" : "—";
        DownloadSpeed = snapshot.DownloadRateBytesPerSecond.HasValue
            ? SizeFormatter.FormatSpeed(snapshot.DownloadRateBytesPerSecond.Value)
            : "—";
        UploadSpeed = snapshot.UploadRateBytesPerSecond.HasValue
            ? SizeFormatter.FormatSpeed(snapshot.UploadRateBytesPerSecond.Value)
            : "—";
        Ratio = snapshot.Ratio.HasValue ? $"{snapshot.Ratio.Value:F2}" : "—";
        Flags = BuildFlags(snapshot);
    }

    private static string BuildFlags(TorrentPeerSnapshot snapshot)
    {
        var flags = new List<string>(3);
        if (snapshot.IsSeeder == true)
            flags.Add("Seeder");
        if (snapshot.IsInterested == true)
            flags.Add("Interested");
        if (snapshot.IsChoking == true)
            flags.Add("Choking");

        return flags.Count == 0 ? "—" : string.Join(", ", flags);
    }
}
