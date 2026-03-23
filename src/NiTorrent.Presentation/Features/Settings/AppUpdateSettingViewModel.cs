using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;

namespace NiTorrent.Presentation.Features.Settings;

public partial class AppUpdateSettingViewModel : ObservableObject
{
    private readonly IAppInfo _appInfo;
    private readonly IAppPreferences _prefs;
    private readonly IUpdateService _updates;
    private readonly IUriLauncher _launcher;
    private readonly IDialogService _dialogs;

    private string _changeLog = string.Empty;
    private Uri? _downloadUri;

    [ObservableProperty]
    private string currentVersion = "";
    [ObservableProperty]
    private string lastUpdateCheck = "Never";
    [ObservableProperty]
    private bool isUpdateAvailable;
    [ObservableProperty]
    private bool isLoading;
    [ObservableProperty]
    private bool isCheckButtonEnabled = true;
    [ObservableProperty]
    private string loadingStatus = "Status";

    public AppUpdateSettingViewModel(
        IAppInfo appInfo,
        IAppPreferences prefs,
        IUpdateService updates,
        IUriLauncher launcher,
        IDialogService dialogs)
    {
        _appInfo = appInfo;
        _prefs = prefs;
        _updates = updates;
        _launcher = launcher;
        _dialogs = dialogs;

        CurrentVersion = $"Current Version {_appInfo.VersionWithPrefix}";
        LastUpdateCheck = FormatLastCheck(_prefs.LastUpdateCheckUtc);
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync(CancellationToken ct)
    {
        IsLoading = true;
        IsUpdateAvailable = false;
        IsCheckButtonEnabled = false;
        LoadingStatus = "Checking for new version";

        try
        {
            _prefs.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            LastUpdateCheck = FormatLastCheck(_prefs.LastUpdateCheckUtc);

            var result = await _updates.CheckAsync(_appInfo.Version, ct);

            _changeLog = result.ChangeLog ?? string.Empty;
            _downloadUri = result.DownloadUri ?? _updates.GetDefaultReleasesUri();

            IsUpdateAvailable = result.IsUpdateAvailable;
            LoadingStatus = result.StatusMessage;
        }
        catch (Exception ex)
        {
            LoadingStatus = UserErrorMapper.ToMessage(ex, "Не удалось проверить обновления.");
        }
        finally
        {
            IsLoading = false;
            IsCheckButtonEnabled = true;
        }
    }

    [RelayCommand]
    private Task GoToUpdateAsync(CancellationToken ct)
    {
        var uri = _downloadUri ?? _updates.GetDefaultReleasesUri();
        return _launcher.LaunchAsync(uri, ct);
    }

    [RelayCommand]
    private Task GetReleaseNotesAsync(CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(_changeLog) ? "No release notes." : _changeLog;
        return _dialogs.ShowTextAsync("Release Note", text, ct);
    }

    private static string FormatLastCheck(DateTimeOffset? utc)
        => utc is null ? "Never" : utc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
