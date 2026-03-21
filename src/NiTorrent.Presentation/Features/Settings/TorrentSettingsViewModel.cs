using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Presentation.Features.Settings;

public partial class TorrentSettingsViewModel : ObservableObject
{
    private readonly ITorrentPreferences _prefs;
    private readonly IAppShellSettingsService _appShellSettingsService;
    private readonly IPickerHelper _picker;
    private readonly ApplyTorrentSettingsUseCase _applyTorrentSettingsUseCase;

    public ObservableCollection<SizeUnit> Units { get; } =
        new() { SizeUnit.B, SizeUnit.KB, SizeUnit.MB, SizeUnit.GB };

    public ObservableCollection<TorrentFastResumeMode> FastResumeModes { get; } =
        new() { TorrentFastResumeMode.BestEffort, TorrentFastResumeMode.Accurate };

    public TorrentSettingsViewModel(
        ITorrentPreferences prefs,
        IAppShellSettingsService appShellSettingsService,
        IPickerHelper picker,
        ApplyTorrentSettingsUseCase applyTorrentSettingsUseCase)
    {
        _prefs = prefs;
        _appShellSettingsService = appShellSettingsService;
        _picker = picker;
        _applyTorrentSettingsUseCase = applyTorrentSettingsUseCase;
        LoadFromPrefs();
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

    private void LoadFromPrefs()
    {
        DefaultDownloadPath = _prefs.DefaultDownloadPath;

        DownloadRateValue = SizeFormatter.ToUnit(_prefs.MaximumDownloadRate, SelectedDownloadUnit);
        UploadRateValue = SizeFormatter.ToUnit(_prefs.MaximumUploadRate, SelectedUploadUnit);

        DiskReadRateValue = SizeFormatter.ToUnit(_prefs.MaximumDiskReadRate, SelectedDiskReadUnit);
        DiskWriteRateValue = SizeFormatter.ToUnit(_prefs.MaximumDiskWriteRate, SelectedDiskWriteUnit);

        AllowDht = _prefs.AllowDht;
        AllowPortForwarding = _prefs.AllowPortForwarding;
        AllowLocalPeerDiscovery = _prefs.AllowLocalPeerDiscovery;

        MaximumConnections = _prefs.MaximumConnections;
        MaximumOpenFiles = _prefs.MaximumOpenFiles;

        AutoSaveLoadFastResume = _prefs.AutoSaveLoadFastResume;
        AutoSaveLoadMagnetLinkMetadata = _prefs.AutoSaveLoadMagnetLinkMetadata;
        SelectedFastResumeMode = _prefs.FastResumeMode;
        MinimizeToTrayOnClose = _appShellSettingsService.GetCloseBehavior() == AppCloseBehavior.MinimizeToTray;
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
        _prefs.DefaultDownloadPath = DefaultDownloadPath;

        _prefs.MaximumDownloadRate = (int)Math.Max(0, SizeFormatter.Parse(DownloadRateValue, SelectedDownloadUnit));
        _prefs.MaximumUploadRate = (int)Math.Max(0, SizeFormatter.Parse(UploadRateValue, SelectedUploadUnit));

        _prefs.MaximumDiskReadRate = (int)Math.Max(0, SizeFormatter.Parse(DiskReadRateValue, SelectedDiskReadUnit));
        _prefs.MaximumDiskWriteRate = (int)Math.Max(0, SizeFormatter.Parse(DiskWriteRateValue, SelectedDiskWriteUnit));

        _prefs.AllowDht = AllowDht;
        _prefs.AllowPortForwarding = AllowPortForwarding;
        _prefs.AllowLocalPeerDiscovery = AllowLocalPeerDiscovery;

        _prefs.MaximumConnections = MaximumConnections;
        _prefs.MaximumOpenFiles = MaximumOpenFiles;

        _prefs.AutoSaveLoadFastResume = AutoSaveLoadFastResume;
        _prefs.AutoSaveLoadMagnetLinkMetadata = AutoSaveLoadMagnetLinkMetadata;
        _prefs.FastResumeMode = SelectedFastResumeMode;

        await _appShellSettingsService.SaveCloseBehaviorAsync(
            MinimizeToTrayOnClose ? AppCloseBehavior.MinimizeToTray : AppCloseBehavior.ExitApplication);

        await _applyTorrentSettingsUseCase.ExecuteAsync();
    }

    [RelayCommand]
    private void Reload()
        => LoadFromPrefs();
}
