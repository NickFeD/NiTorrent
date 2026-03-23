using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

/// <summary>
/// Details view model backed by dedicated query/workflow slices instead of sharing the list read feed.
/// </summary>
public partial class TorrentDetailsViewModel : ObservableObject
{
    private readonly GetTorrentDetailsQuery _detailsQuery;
    private readonly UpdatePerTorrentSettingsWorkflow _updateSettingsWorkflow;
    private readonly IDialogService _dialogs;
    private TorrentId _currentTorrentId;

    public TorrentDetailsViewModel(GetTorrentDetailsQuery detailsQuery, UpdatePerTorrentSettingsWorkflow updateSettingsWorkflow, IDialogService dialogs)
    {
        _detailsQuery = detailsQuery;
        _updateSettingsWorkflow = updateSettingsWorkflow;
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

    public async Task LoadAsync(TorrentId torrentId)
    {
        var details = await _detailsQuery.ExecuteAsync(torrentId);
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
        Title = details.Name;
        SavePath = details.SavePath;
        StatusLabel = details.Status.Phase.ToString();
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
        try
        {
            await _updateSettingsWorkflow.ExecuteAsync(_currentTorrentId, settings);
            await _dialogs.ShowTextAsync("Настройки торрента", "Настройки торрента сохранены и применены там, где это возможно без перезапуска.");
        }
        catch (Exception ex)
        {
            await _dialogs.ShowTextAsync("Настройки торрента", UserErrorMapper.ToMessage(ex, "Не удалось сохранить настройки торрента."));
        }
    }
}
