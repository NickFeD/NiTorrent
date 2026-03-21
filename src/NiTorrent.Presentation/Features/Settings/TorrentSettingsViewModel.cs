using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;
using NiTorrent.Application.Settings;

namespace NiTorrent.Presentation.Features.Settings;

public partial class TorrentSettingsViewModel : ObservableObject
{
    private readonly ITorrentSettingsService _settingsService;
    private readonly IPickerHelper _picker;
    private bool _isLoading;

    public ObservableCollection<SizeUnit> Units { get; } =
        new() { SizeUnit.B, SizeUnit.KB, SizeUnit.MB, SizeUnit.GB };

    public ObservableCollection<TorrentFastResumeMode> FastResumeModes { get; } =
        new() { TorrentFastResumeMode.BestEffort, TorrentFastResumeMode.Accurate };

    public TorrentSettingsViewModel(ITorrentSettingsService settingsService, IPickerHelper picker)
    {
        _settingsService = settingsService;
        _picker = picker;

        PropertyChanged += OnViewModelPropertyChanged;
        _ = ReloadAsync();
    }

    // --- UI поля ---
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

    private async Task ReloadAsync(CancellationToken ct = default)
    {
        _isLoading = true;
        try
        {
            var settings = await _settingsService.LoadAsync(ct);
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

    private bool CanSave() => HasUnsavedChanges;

    [RelayCommand(CanExecute = nameof(CanSave))]
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

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task Reload()
        => ReloadAsync();

    partial void OnHasUnsavedChangesChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isLoading || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName is nameof(HasUnsavedChanges))
            return;

        HasUnsavedChanges = true;
    }
}
