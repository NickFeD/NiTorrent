using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.Presentation.Features.Torrents;

/// <summary>
/// Foundation for the future double-click details page.
/// Not wired into navigation yet.
/// </summary>
public partial class TorrentDetailsViewModel : ObservableObject
{
    private readonly ITorrentDetailsService _detailsService;
    private readonly IDialogService _dialogs;
    private TorrentId _currentTorrentId;

    public TorrentDetailsViewModel(ITorrentDetailsService detailsService, IDialogService dialogs)
    {
        _detailsService = detailsService;
        _dialogs = dialogs;
    }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SavePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? DownloadPathOverride { get; set; }

    [ObservableProperty]
    public partial int? MaximumDownloadRateBytesPerSecond { get; set; }

    [ObservableProperty]
    public partial int? MaximumUploadRateBytesPerSecond { get; set; }

    [ObservableProperty]
    public partial bool SequentialDownload { get; set; }

    public bool HasTorrent => _currentTorrentId != TorrentId.Empty;

    public void Load(TorrentId torrentId)
    {
        var details = _detailsService.Get(torrentId);
        if (details is null)
        {
            _currentTorrentId = TorrentId.Empty;
            Title = string.Empty;
            SavePath = string.Empty;
            StatusLabel = string.Empty;
            DownloadPathOverride = null;
            MaximumDownloadRateBytesPerSecond = null;
            MaximumUploadRateBytesPerSecond = null;
            SequentialDownload = false;
            OnPropertyChanged(nameof(HasTorrent));
            return;
        }

        _currentTorrentId = torrentId;
        Title = details.Snapshot.Name;
        SavePath = details.Snapshot.SavePath;
        StatusLabel = details.Snapshot.Status.Phase.ToString();
        DownloadPathOverride = details.Settings.DownloadPathOverride;
        MaximumDownloadRateBytesPerSecond = details.Settings.MaximumDownloadRateBytesPerSecond;
        MaximumUploadRateBytesPerSecond = details.Settings.MaximumUploadRateBytesPerSecond;
        SequentialDownload = details.Settings.SequentialDownload;
        OnPropertyChanged(nameof(HasTorrent));
    }

    [RelayCommand]
    private Task SaveAsync()
    {
        if (_currentTorrentId == TorrentId.Empty)
            return Task.CompletedTask;

        var settings = new TorrentEntrySettings
        {
            DownloadPathOverride = string.IsNullOrWhiteSpace(DownloadPathOverride) ? null : DownloadPathOverride,
            MaximumDownloadRateBytesPerSecond = MaximumDownloadRateBytesPerSecond,
            MaximumUploadRateBytesPerSecond = MaximumUploadRateBytesPerSecond,
            SequentialDownload = SequentialDownload
        };

        _detailsService.SaveSettings(_currentTorrentId, settings);
        return _dialogs.ShowTextAsync("Настройки торрента", "Настройки торрента сохранены.");
    }
}
