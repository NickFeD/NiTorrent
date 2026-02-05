using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using MonoTorrent.Client;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.App.Converters;

public partial class TorrentStateToBadgeStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TorrentStatus info)
            return App.Current.Resources["TorrentStoppedBadgeStyle"];
        if (info.IsComplete) return App.Current.Resources["TorrentCompletedBadgeStyle"];

        return info.Phase switch
        {
            TorrentPhase.Stopped => App.Current.Resources["TorrentStoppedBadgeStyle"],
            TorrentPhase.Paused => App.Current.Resources["TorrentPausedBadgeStyle"],
            TorrentPhase.Downloading => App.Current.Resources["TorrentDownloadingBadgeStyle"],
            TorrentPhase.Seeding => App.Current.Resources["TorrentSeedingBadgeStyle"],
            TorrentPhase.Checking => App.Current.Resources["TorrentStartingBadgeStyle"],
            TorrentPhase.FetchingMetadata => App.Current.Resources["TorrentMetadataBadgeStyle"],
            TorrentPhase.Error => App.Current.Resources["TorrentErrorBadgeStyle"],
            _ => App.Current.Resources["TorrentStoppedBadgeStyle"]
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}



