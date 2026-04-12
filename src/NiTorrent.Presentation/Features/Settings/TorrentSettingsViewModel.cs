using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Domain.Settings;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.Presentation.Features.Settings;

public partial class TorrentSettingsViewModel : ObservableObject
{
    private readonly ITorrentSettingsService _settingsService;
    private readonly GetSettingsQuery _getSettingsQuery;
    private readonly IPickerHelper _picker;

    public ObservableCollection<SizeUnit> Units { get; } =
        new() { SizeUnit.B, SizeUnit.KB, SizeUnit.MB, SizeUnit.GB };

    public ObservableCollection<TorrentFastResumeMode> FastResumeModes { get; } =
        new() { TorrentFastResumeMode.BestEffort, TorrentFastResumeMode.Accurate };

    public TorrentSettingsViewModel(ITorrentSettingsService settingsService, GetSettingsQuery getSettingsQuery, IPickerHelper picker)
    {
        _settingsService = settingsService;
        _getSettingsQuery = getSettingsQuery;
        _picker = picker;
    }

    [ObservableProperty] public partial string DefaultDownloadPath { get; set; } = "";

    [ObservableProperty] public partial double DownloadRateValue { get; set; }
    [ObservableProperty] public partial SizeUnit SelectedDownloadUnit { get; set; } = SizeUnit.MB;

    [ObservableProperty] public partial double UploadRateValue { get; set; }
    [ObservableProperty] public partial SizeUnit SelectedUploadUnit { get; set; } = SizeUnit.MB;

    [ObservableProperty] public partial double DiskReadRateValue { get; set; }
    [ObservableProperty] public partial SizeUnit SelectedDiskReadUnit { get; set; } = SizeUnit.MB;

    [ObservableProperty] public partial double DiskWriteRateValue { get; set; }
    [ObservableProperty] public partial SizeUnit SelectedDiskWriteUnit { get; set; } = SizeUnit.MB;

    [ObservableProperty] public partial bool AllowDht { get; set; }
    [ObservableProperty] public partial bool AllowPortForwarding { get; set; }
    [ObservableProperty] public partial bool AllowLocalPeerDiscovery { get; set; }

    [ObservableProperty] public partial int MaximumConnections { get; set; }
    [ObservableProperty] public partial int MaximumOpenFiles { get; set; }

    [ObservableProperty] public partial bool AutoSaveLoadFastResume { get; set; }
    [ObservableProperty] public partial bool AutoSaveLoadMagnetLinkMetadata { get; set; }
    [ObservableProperty] public partial TorrentFastResumeMode SelectedFastResumeMode { get; set; } = TorrentFastResumeMode.BestEffort;
    [ObservableProperty] public partial bool MinimizeToTrayOnClose { get; set; }
    [ObservableProperty] public partial bool HasUnsavedChanges { get; set; }

    private bool _isLoading;
    private bool _initialized;

    partial void OnDefaultDownloadPathChanged(string value) => MarkDirty();
    partial void OnDownloadRateValueChanged(double value) => MarkDirty();
    partial void OnSelectedDownloadUnitChanged(SizeUnit value) => MarkDirty();
    partial void OnUploadRateValueChanged(double value) => MarkDirty();
    partial void OnSelectedUploadUnitChanged(SizeUnit value) => MarkDirty();
    partial void OnDiskReadRateValueChanged(double value) => MarkDirty();
    partial void OnSelectedDiskReadUnitChanged(SizeUnit value) => MarkDirty();
    partial void OnDiskWriteRateValueChanged(double value) => MarkDirty();
    partial void OnSelectedDiskWriteUnitChanged(SizeUnit value) => MarkDirty();
    partial void OnAllowDhtChanged(bool value) => MarkDirty();
    partial void OnAllowPortForwardingChanged(bool value) => MarkDirty();
    partial void OnAllowLocalPeerDiscoveryChanged(bool value) => MarkDirty();
    partial void OnMaximumConnectionsChanged(int value) => MarkDirty();
    partial void OnMaximumOpenFilesChanged(int value) => MarkDirty();
    partial void OnAutoSaveLoadFastResumeChanged(bool value) => MarkDirty();
    partial void OnAutoSaveLoadMagnetLinkMetadataChanged(bool value) => MarkDirty();
    partial void OnSelectedFastResumeModeChanged(TorrentFastResumeMode value) => MarkDirty();
    partial void OnMinimizeToTrayOnCloseChanged(bool value) => MarkDirty();

    private void MarkDirty()
    {
        if (!_isLoading)
            HasUnsavedChanges = true;
    }

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return;

        await LoadFromSettingsAsync(ct);
        _initialized = true;
    }

    private async Task LoadFromSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _getSettingsQuery.ExecuteAsync(ct);

        _isLoading = true;
        try
        {
            DefaultDownloadPath = settings.DefaultDownloadPath;

            DownloadRateValue = SizeFormatter.ToUnit(settings.MaximumDownloadRate, SelectedDownloadUnit);
            UploadRateValue = SizeFormatter.ToUnit(settings.MaximumUploadRate, SelectedUploadUnit);
            DiskReadRateValue = SizeFormatter.ToUnit(settings.MaximumDiskReadRate, SelectedDiskReadUnit);
            DiskWriteRateValue = SizeFormatter.ToUnit(settings.MaximumDiskWriteRate, SelectedDiskWriteUnit);

            AllowDht = settings.AllowDht;
            AllowPortForwarding = settings.AllowPortForwarding;
            AllowLocalPeerDiscovery = settings.AllowLocalPeerDiscovery;

            MaximumConnections = settings.MaximumConnections;
            MaximumOpenFiles = settings.MaximumOpenFiles;

            AutoSaveLoadFastResume = settings.AutoSaveLoadFastResume;
            AutoSaveLoadMagnetLinkMetadata = settings.AutoSaveLoadMagnetLinkMetadata;
            SelectedFastResumeMode = settings.FastResumeMode;
            MinimizeToTrayOnClose = settings.CloseBehavior == AppCloseBehavior.MinimizeToTray;
            HasUnsavedChanges = false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task BrowseDefaultPathAsync()
    {
        var folder = await _picker.PickSingleFolderPathAsync();
        if (!string.IsNullOrWhiteSpace(folder))
            DefaultDownloadPath = folder;
    }

    [RelayCommand]
    private async Task Save()
    {
        var settings = new TorrentSettingsDraft(
            DefaultDownloadPath,
            (int)Math.Max(0, SizeFormatter.Parse(DownloadRateValue, SelectedDownloadUnit)),
            (int)Math.Max(0, SizeFormatter.Parse(UploadRateValue, SelectedUploadUnit)),
            (int)Math.Max(0, SizeFormatter.Parse(DiskReadRateValue, SelectedDiskReadUnit)),
            (int)Math.Max(0, SizeFormatter.Parse(DiskWriteRateValue, SelectedDiskWriteUnit)),
            AllowDht,
            AllowPortForwarding,
            AllowLocalPeerDiscovery,
            MaximumConnections,
            MaximumOpenFiles,
            AutoSaveLoadFastResume,
            AutoSaveLoadMagnetLinkMetadata,
            SelectedFastResumeMode,
            MinimizeToTrayOnClose ? AppCloseBehavior.MinimizeToTray : AppCloseBehavior.ExitApplication);

        await _settingsService.SaveAndApplyAsync(settings);
        HasUnsavedChanges = false;
    }

    [RelayCommand]
    private Task ReloadAsync()
        => LoadFromSettingsAsync();
}
