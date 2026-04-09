using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Queries;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentDetailsViewModel : ObservableObject
{
    private static readonly TimeSpan LiveRefreshInterval = TimeSpan.FromSeconds(1);
    private const int MaxChartPoints = 180;

    private readonly GetTorrentDetailsQuery _detailsQuery;
    private readonly GetTorrentRuntimeDetailsQuery _runtimeQuery;
    private readonly UpdatePerTorrentSettingsWorkflow _updateSettingsWorkflow;
    private readonly ITorrentWorkflowService _torrentWorkflowService;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;

    private readonly Dictionary<string, TorrentPeerItemViewModel> _peersByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TorrentTrackerItemViewModel> _trackersByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SpeedSamplePoint> _speedHistory = [];

    private CancellationTokenSource? _liveCts;
    private TorrentId _currentTorrentId;
    private long _knownTotalSize;

    public TorrentDetailsViewModel(
        GetTorrentDetailsQuery detailsQuery,
        GetTorrentRuntimeDetailsQuery runtimeQuery,
        UpdatePerTorrentSettingsWorkflow updateSettingsWorkflow,
        ITorrentWorkflowService torrentWorkflowService,
        IDialogService dialogs,
        IUiDispatcher ui)
    {
        _detailsQuery = detailsQuery;
        _runtimeQuery = runtimeQuery;
        _updateSettingsWorkflow = updateSettingsWorkflow;
        _torrentWorkflowService = torrentWorkflowService;
        _dialogs = dialogs;
        _ui = ui;
    }

    public ObservableCollection<TorrentPeerItemViewModel> Peers { get; } = [];
    public ObservableCollection<TorrentTrackerItemViewModel> Trackers { get; } = [];
    public IReadOnlyList<SpeedSamplePoint> SpeedHistory => _speedHistory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SavePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Hash { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusLabel { get; set; } = "Unknown";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial double ProgressPercent { get; set; }

    public string ProgressText => $"{ProgressPercent:F1}%";

    [ObservableProperty] public partial string ProgressSummaryText { get; set; } = "0 B / 0 B";
    [ObservableProperty] public partial string DownloadSpeedText { get; set; } = "0 B/s";
    [ObservableProperty] public partial string UploadSpeedText { get; set; } = "0 B/s";
    [ObservableProperty] public partial string EtaText { get; set; } = "—";
    [ObservableProperty] public partial string RatioText { get; set; } = "0.00";
    [ObservableProperty] public partial string SizeText { get; set; } = "0 B";
    [ObservableProperty] public partial string RemainingText { get; set; } = "0 B";
    [ObservableProperty] public partial string DownloadedText { get; set; } = "0 B";
    [ObservableProperty] public partial string UploadedText { get; set; } = "0 B";
    [ObservableProperty] public partial string AddedAtText { get; set; } = "—";
    [ObservableProperty] public partial string PieceSizeText { get; set; } = "—";
    [ObservableProperty] public partial string PieceCountText { get; set; } = "—";
    [ObservableProperty] public partial string HashFailsText { get; set; } = "—";
    [ObservableProperty] public partial string UnhashedPiecesText { get; set; } = "—";
    [ObservableProperty] public partial string ConnectionsText { get; set; } = "—";
    [ObservableProperty] public partial string UploadingToText { get; set; } = "—";
    [ObservableProperty] public partial string PeersCountText { get; set; } = "—";
    [ObservableProperty] public partial string SeedsCountText { get; set; } = "—";
    [ObservableProperty] public partial string TrackerCountText { get; set; } = "0";
    [ObservableProperty] public partial string HasMetadataText { get; set; } = "No";
    [ObservableProperty] public partial ulong DownloadGraphTotal { get; set; }
    [ObservableProperty] public partial ulong UploadGraphTotal { get; set; }
    [ObservableProperty] public partial ulong DownloadGraphMaxSpeed { get; set; } = 1;
    [ObservableProperty] public partial ulong UploadGraphMaxSpeed { get; set; } = 1;

    [ObservableProperty]
    public partial string? DownloadPathOverride { get; set; }

    [ObservableProperty]
    public partial string? MaximumDownloadRateText { get; set; }

    [ObservableProperty]
    public partial string? MaximumUploadRateText { get; set; }

    [ObservableProperty]
    public partial bool SequentialDownload { get; set; }

    [ObservableProperty]
    public partial bool IsLiveUpdating { get; set; }

    public bool HasTorrent => _currentTorrentId != TorrentId.Empty;
    public bool CanSave => HasTorrent;

    private TorrentPhase CurrentPhase { get; set; } = TorrentPhase.Unknown;

    private bool CanStart()
        => HasTorrent && CurrentPhase is TorrentPhase.Stopped or TorrentPhase.Paused or TorrentPhase.Error;

    private bool CanPause()
        => HasTorrent && CurrentPhase is TorrentPhase.WaitingForEngine or TorrentPhase.FetchingMetadata or TorrentPhase.Checking or TorrentPhase.Downloading or TorrentPhase.Seeding;

    private bool CanOpenFolder()
        => HasTorrent && !string.IsNullOrWhiteSpace(SavePath);

    private bool CanDelete()
        => HasTorrent;

    public async Task LoadAsync(TorrentId torrentId)
    {
        var details = await _detailsQuery.ExecuteAsync(torrentId).ConfigureAwait(false);
        if (details is null)
        {
            await _ui.EnqueueAsync(ResetToEmptyState).ConfigureAwait(false);
            return;
        }

        _currentTorrentId = torrentId;
        _knownTotalSize = Math.Max(0, details.Size);

        await _ui.EnqueueAsync(() =>
        {
            Title = details.Name;
            SavePath = details.SavePath;
            Hash = details.Key;
            AddedAtText = details.AddedAtUtc.ToLocalTime().ToString("g");
            HasMetadataText = details.HasMetadata ? "Yes" : "No";
            StatusLabel = TorrentStatusTextMapper.ToUserFacingText(details.Status);
            CurrentPhase = details.Status.Phase;
            ProgressPercent = details.Status.Progress;
            SizeText = SizeFormatter.FormatBytes(_knownTotalSize);
            DownloadPathOverride = details.Settings.DownloadPathOverride;
            MaximumDownloadRateText = details.Settings.MaximumDownloadRateBytesPerSecond?.ToString();
            MaximumUploadRateText = details.Settings.MaximumUploadRateBytesPerSecond?.ToString();
            SequentialDownload = details.Settings.SequentialDownload;
            UpdateCommandStates();
            OnPropertyChanged(nameof(HasTorrent));
            OnPropertyChanged(nameof(CanSave));
        }).ConfigureAwait(false);

        await RefreshRuntimeAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public void Activate()
    {
        if (!HasTorrent || IsLiveUpdating)
            return;

        IsLiveUpdating = true;
        _liveCts = new CancellationTokenSource();
        _ = Task.Run(() => LiveLoopAsync(_liveCts.Token));
    }

    public void Deactivate()
    {
        IsLiveUpdating = false;
        _liveCts?.Cancel();
        _liveCts?.Dispose();
        _liveCts = null;
    }

    private async Task LiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RefreshRuntimeAsync(ct).ConfigureAwait(false);

            try
            {
                await Task.Delay(LiveRefreshInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshRuntimeAsync(CancellationToken ct)
    {
        if (!HasTorrent)
            return;

        TorrentRuntimeDetailsSnapshot? snapshot;
        try
        {
            snapshot = await _runtimeQuery.ExecuteAsync(_currentTorrentId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            return;
        }

        if (snapshot is null || snapshot.Id != _currentTorrentId)
            return;

        _ui.TryEnqueue(() => ApplyRuntimeSnapshot(snapshot));
    }

    private void ApplyRuntimeSnapshot(TorrentRuntimeDetailsSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.StableKey))
            Hash = snapshot.StableKey;

        CurrentPhase = snapshot.Status.Phase;
        StatusLabel = TorrentStatusTextMapper.ToUserFacingText(snapshot.Status);
        ProgressPercent = snapshot.Status.Progress;
        DownloadSpeedText = SizeFormatter.FormatSpeed(snapshot.Status.DownloadRateBytesPerSecond);
        UploadSpeedText = SizeFormatter.FormatSpeed(snapshot.Status.UploadRateBytesPerSecond);
        DownloadGraphTotal = (ulong)Math.Max(0, snapshot.Status.DownloadRateBytesPerSecond);
        UploadGraphTotal = (ulong)Math.Max(0, snapshot.Status.UploadRateBytesPerSecond);
        EtaText = FormatEta(snapshot.Eta);
        RatioText = $"{snapshot.Ratio:F2}";

        var totalSize = snapshot.TotalSizeBytes.GetValueOrDefault(_knownTotalSize);
        if (totalSize <= 0)
            totalSize = _knownTotalSize;

        SizeText = SizeFormatter.FormatBytes(totalSize);
        DownloadedText = SizeFormatter.FormatBytes(snapshot.DownloadedBytes);
        UploadedText = SizeFormatter.FormatBytes(snapshot.UploadedBytes);
        RemainingText = SizeFormatter.FormatBytes(snapshot.RemainingBytes);
        ProgressSummaryText = $"{DownloadedText} / {SizeText}";

        PieceSizeText = snapshot.PieceSizeBytes.HasValue
            ? SizeFormatter.FormatBytes(snapshot.PieceSizeBytes.Value)
            : "—";
        PieceCountText = snapshot.PieceCount?.ToString("N0") ?? "—";
        HashFailsText = snapshot.HashFailCount?.ToString("N0") ?? "—";
        UnhashedPiecesText = snapshot.UnhashedPieceCount?.ToString("N0") ?? "—";
        ConnectionsText = snapshot.OpenConnections?.ToString("N0") ?? "—";
        UploadingToText = snapshot.UploadingToConnections?.ToString("N0") ?? "—";
        PeersCountText = snapshot.PeerCount?.ToString("N0") ?? "—";
        SeedsCountText = snapshot.SeedCount?.ToString("N0") ?? "—";
        TrackerCountText = snapshot.Trackers.Count.ToString("N0");

        AppendSpeedSample(snapshot.Status.DownloadRateBytesPerSecond, snapshot.Status.UploadRateBytesPerSecond);
        ApplyPeers(snapshot.Peers);
        ApplyTrackers(snapshot.Trackers);
        UpdateCommandStates();
    }

    private void AppendSpeedSample(long downloadRateBytesPerSecond, long uploadRateBytesPerSecond)
    {
        _speedHistory.Add(new SpeedSamplePoint(downloadRateBytesPerSecond, uploadRateBytesPerSecond));
        if (_speedHistory.Count > MaxChartPoints)
            _speedHistory.RemoveAt(0);

        DownloadGraphMaxSpeed = (ulong)Math.Max(1L, _speedHistory.Max(x => x.DownloadRateBytesPerSecond));
        UploadGraphMaxSpeed = (ulong)Math.Max(1L, _speedHistory.Max(x => x.UploadRateBytesPerSecond));

        OnPropertyChanged(nameof(SpeedHistory));
    }

    private void ApplyPeers(IReadOnlyList<TorrentPeerSnapshot> peers)
    {
        var actualKeys = peers.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _peersByKey.Keys.Where(key => !actualKeys.Contains(key)).ToList())
        {
            var staleVm = _peersByKey[stale];
            _peersByKey.Remove(stale);
            Peers.Remove(staleVm);
        }

        foreach (var peer in peers)
        {
            if (_peersByKey.TryGetValue(peer.Key, out var existing))
            {
                existing.Update(peer);
            }
            else
            {
                var vm = new TorrentPeerItemViewModel(peer);
                _peersByKey.Add(peer.Key, vm);
                Peers.Add(vm);
            }
        }
    }

    private void ApplyTrackers(IReadOnlyList<TorrentTrackerSnapshot> trackers)
    {
        var actualKeys = trackers.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _trackersByKey.Keys.Where(key => !actualKeys.Contains(key)).ToList())
        {
            var staleVm = _trackersByKey[stale];
            _trackersByKey.Remove(stale);
            Trackers.Remove(staleVm);
        }

        foreach (var tracker in trackers)
        {
            if (_trackersByKey.TryGetValue(tracker.Key, out var existing))
            {
                existing.Update(tracker);
            }
            else
            {
                var vm = new TorrentTrackerItemViewModel(tracker);
                _trackersByKey.Add(tracker.Key, vm);
                Trackers.Add(vm);
            }
        }
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

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!HasTorrent)
            return;

        try
        {
            await _torrentWorkflowService.StartAsync(_currentTorrentId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowTextAsync("Start torrent", UserErrorMapper.ToMessage(ex, "Failed to start torrent.")).ConfigureAwait(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        if (!HasTorrent)
            return;

        try
        {
            await _torrentWorkflowService.PauseAsync(_currentTorrentId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowTextAsync("Pause torrent", UserErrorMapper.ToMessage(ex, "Failed to pause torrent.")).ConfigureAwait(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private async Task OpenFolderAsync()
    {
        if (!HasTorrent)
            return;

        try
        {
            await _torrentWorkflowService.OpenFolderAsync(SavePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowTextAsync("Open folder", UserErrorMapper.ToMessage(ex, "Failed to open torrent folder.")).ConfigureAwait(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteAsync()
        => DeleteCoreAsync(deleteData: false);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteWithDataAsync()
        => DeleteCoreAsync(deleteData: true);

    private async Task DeleteCoreAsync(bool deleteData)
    {
        if (!HasTorrent)
            return;

        try
        {
            await _torrentWorkflowService.RemoveAsync(_currentTorrentId, deleteData).ConfigureAwait(false);
            await _ui.EnqueueAsync(ResetToEmptyState).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowTextAsync("Delete torrent", UserErrorMapper.ToMessage(ex, "Failed to delete torrent.")).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private Task SaveAsync()
    {
        if (!HasTorrent)
            return Task.CompletedTask;

        if (!TryParseNullableInt(MaximumDownloadRateText, out var maxDownload) || !TryParseNullableInt(MaximumUploadRateText, out var maxUpload))
            return _dialogs.ShowTextAsync("Torrent settings", "Speed limits should be empty or non-negative integers.");

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
            await _updateSettingsWorkflow.ExecuteAsync(_currentTorrentId, settings).ConfigureAwait(false);
            await _dialogs.ShowTextAsync("Torrent settings", "Torrent settings were saved.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowTextAsync("Torrent settings", UserErrorMapper.ToMessage(ex, "Failed to save torrent settings.")).ConfigureAwait(false);
        }
    }

    private void ResetToEmptyState()
    {
        Deactivate();
        _currentTorrentId = TorrentId.Empty;
        _knownTotalSize = 0;
        CurrentPhase = TorrentPhase.Unknown;
        Title = string.Empty;
        SavePath = string.Empty;
        Hash = string.Empty;
        StatusLabel = "Unknown";
        ProgressPercent = 0;
        ProgressSummaryText = "0 B / 0 B";
        DownloadSpeedText = "0 B/s";
        UploadSpeedText = "0 B/s";
        DownloadGraphTotal = 0;
        UploadGraphTotal = 0;
        DownloadGraphMaxSpeed = 1;
        UploadGraphMaxSpeed = 1;
        EtaText = "—";
        RatioText = "0.00";
        SizeText = "0 B";
        RemainingText = "0 B";
        DownloadedText = "0 B";
        UploadedText = "0 B";
        AddedAtText = "—";
        PieceSizeText = "—";
        PieceCountText = "—";
        HashFailsText = "—";
        UnhashedPiecesText = "—";
        ConnectionsText = "—";
        UploadingToText = "—";
        PeersCountText = "—";
        SeedsCountText = "—";
        TrackerCountText = "0";
        HasMetadataText = "No";
        DownloadPathOverride = null;
        MaximumDownloadRateText = null;
        MaximumUploadRateText = null;
        SequentialDownload = false;
        _speedHistory.Clear();
        OnPropertyChanged(nameof(SpeedHistory));
        _peersByKey.Clear();
        _trackersByKey.Clear();
        Peers.Clear();
        Trackers.Clear();
        OnPropertyChanged(nameof(HasTorrent));
        OnPropertyChanged(nameof(CanSave));
        UpdateCommandStates();
    }

    private void UpdateCommandStates()
    {
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        DeleteWithDataCommand.NotifyCanExecuteChanged();
    }

    private static string FormatEta(TimeSpan? eta)
    {
        if (!eta.HasValue || eta.Value <= TimeSpan.Zero || eta.Value == TimeSpan.MaxValue)
            return "—";

        if (eta.Value.TotalDays >= 1)
            return $"{(int)eta.Value.TotalDays}d {eta.Value.Hours}h";

        if (eta.Value.TotalHours >= 1)
            return $"{(int)eta.Value.TotalHours}h {eta.Value.Minutes}m";

        if (eta.Value.TotalMinutes >= 1)
            return $"{(int)eta.Value.TotalMinutes}m {eta.Value.Seconds}s";

        return $"{eta.Value.Seconds}s";
    }
}
