using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.Presentation.Features.Torrents;

/// <summary>
/// Details view model for the torrent details page and per-torrent settings.
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
    public partial string? MaximumDownloadRateText { get; set; }

    [ObservableProperty]
    public partial string? MaximumUploadRateText { get; set; }

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
            MaximumDownloadRateText = null;
            MaximumUploadRateText = null;
            SequentialDownload = false;
            OnPropertyChanged(nameof(HasTorrent));
            return;
        }

        _currentTorrentId = torrentId;
        Title = details.Snapshot.Name;
        SavePath = details.Snapshot.SavePath;
        StatusLabel = details.Snapshot.Status.Phase.ToString();
        DownloadPathOverride = details.Settings.DownloadPathOverride;
        MaximumDownloadRateText = details.Settings.MaximumDownloadRateBytesPerSecond?.ToString();
        MaximumUploadRateText = details.Settings.MaximumUploadRateBytesPerSecond?.ToString();
        SequentialDownload = details.Settings.SequentialDownload;
        OnPropertyChanged(nameof(HasTorrent));
    }

    private static bool TryParseNullableInt(string? value, out int? parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }

        if (int.TryParse(value, out var parsedValue) && parsedValue >= 0)
        {
            parsed = parsedValue;
            return true;
        }

        parsed = null;
        return false;
    }

    [RelayCommand]
    private Task SaveAsync()
    {
        if (_currentTorrentId == TorrentId.Empty)
            return Task.CompletedTask;

        if (!TryParseNullableInt(MaximumDownloadRateText, out var maxDownload) || !TryParseNullableInt(MaximumUploadRateText, out var maxUpload))
            return _dialogs.ShowTextAsync("Настройки торрента", "Лимиты скоростей должны быть пустыми или целыми числами.");

        var settings = new TorrentEntrySettings
        {
            DownloadPathOverride = string.IsNullOrWhiteSpace(DownloadPathOverride) ? null : DownloadPathOverride,
            MaximumDownloadRateBytesPerSecond = maxDownload,
            MaximumUploadRateBytesPerSecond = maxUpload,
            SequentialDownload = SequentialDownload
        };

                return SaveCoreAsync(settings);
    }
    private async Task SaveCoreAsync(TorrentEntrySettings settings)
    {
        await _detailsService.SaveSettingsAsync(_currentTorrentId, settings).ConfigureAwait(false);
        await _dialogs.ShowTextAsync("Настройки торрента", "Настройки торрента сохранены и применены там, где это возможно без перезапуска.").ConfigureAwait(false);
    }

}
