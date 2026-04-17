using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.App.Converters;

public partial class TorrentStateToBadgeStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return new object();
        //if (value is not TorrentStatus info)
        //    return App.Current.Resources["TorrentStoppedBadgeStyle"];
        //if (info.IsComplete) return App.Current.Resources["TorrentCompletedBadgeStyle"];
        //return App.Current.Resources["TorrentStartingBadgeStyle"];
        //return info.Phase switch
        //{
        //    TorrentLifecycleState.EngineStarting => App.Current.Resources["TorrentStartingBadgeStyle"],
        //    TorrentLifecycleState.WaitingForEngine => App.Current.Resources["TorrentStartingBadgeStyle"],
        //    TorrentLifecycleState.Stopped => App.Current.Resources["TorrentStoppedBadgeStyle"],
        //    TorrentLifecycleState.Paused => App.Current.Resources["TorrentStoppedBadgeStyle"],
        //    TorrentLifecycleState.Downloading => App.Current.Resources["TorrentDownloadingBadgeStyle"],
        //    TorrentLifecycleState.Seeding => App.Current.Resources["TorrentSeedingBadgeStyle"],
        //    TorrentLifecycleState.Checking => App.Current.Resources["TorrentStartingBadgeStyle"],
        //    TorrentLifecycleState.FetchingMetadata => App.Current.Resources["TorrentMetadataBadgeStyle"],
        //    TorrentLifecycleState.Error => App.Current.Resources["TorrentErrorBadgeStyle"],
        //    _ => App.Current.Resources["TorrentStoppedBadgeStyle"]
        //};
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value;
}


