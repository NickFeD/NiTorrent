using CommunityToolkit.Mvvm.ComponentModel;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

public partial class TorrentItemViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;
    public TorrentId Id => _item.Id;

    private TorrentListItemReadModel _item;
    public string Size => SizeFormatter.FormatBytes(_item.Size);
    public string Name => _item.Name;
    public string SavePath => _item.SavePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial double Progress { get; set; }

    public string ProgressText => $"{Progress:F1}%";

    [ObservableProperty]
    public partial TorrentStatus State { get; set; } = new(TorrentPhase.Unknown, false, 0, 0, 0);

    [ObservableProperty]
    public partial string StateText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial string DownloadSpeed { get; set; } = "0 B";

    [ObservableProperty]
    public partial string UploadSpeed { get; set; } = "0 B";

    public TorrentItemViewModel(TorrentListItemReadModel item)
    {
        _item = item;
        Update(item);
    }

    public void Update(TorrentListItemReadModel item)
    {
        if (_isDisposed)
            return;

        _item = item;

        State = _item.Status;
        Progress = _item.Status.Progress;
        IsCompleted = _item.Status.IsComplete;
        DownloadSpeed = SizeFormatter.FormatSpeed(_item.Status.DownloadRateBytesPerSecond);
        UploadSpeed = SizeFormatter.FormatSpeed(_item.Status.UploadRateBytesPerSecond);
        StateText = BuildStateText(_item.Status);
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(SavePath));
    }

    private static string BuildStateText(TorrentStatus status)
    {        var sourceSuffix = status.Source == TorrentStatusSource.Cached ? " (РєСЌС€)" : string.Empty;

        return status.Phase switch
        {
            TorrentPhase.EngineStarting => $"Р—Р°РїСѓСЃРє РґРІРёР¶РєР°{sourceSuffix}",
            TorrentPhase.WaitingForEngine => $"РћР¶РёРґР°РµС‚ Р·Р°РїСѓСЃРє РґРІРёР¶РєР°{sourceSuffix}",
            TorrentPhase.FetchingMetadata => $"РџРѕР»СѓС‡РµРЅРёРµ РјРµС‚Р°РґР°РЅРЅС‹С…{sourceSuffix}",
            TorrentPhase.Checking => $"РџСЂРѕРІРµСЂРєР° С„Р°Р№Р»РѕРІ{sourceSuffix}",
            TorrentPhase.Downloading => $"РЎРєР°С‡РёРІР°РЅРёРµ{sourceSuffix}",
            TorrentPhase.Seeding => $"Р Р°Р·РґР°С‡Р°{sourceSuffix}",
            TorrentPhase.Paused => $"Пауза/остановлен{sourceSuffix}",
            TorrentPhase.Stopped => $"Пауза/остановлен{sourceSuffix}",
            TorrentPhase.Error => $"РћС€РёР±РєР°{sourceSuffix}",
            _ => $"РќРµРёР·РІРµСЃС‚РЅРѕ{sourceSuffix}"
        };
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}



