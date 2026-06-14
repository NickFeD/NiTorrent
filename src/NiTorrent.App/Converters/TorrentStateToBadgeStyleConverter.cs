using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.Enum;

namespace NiTorrent.App.Converters;

public partial class TorrentStateToBadgeStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visual = GetVisual(value);

        if (targetType == typeof(string))
            return visual.Glyph;

        if (typeof(Brush).IsAssignableFrom(targetType))
            return App.Current.Resources[visual.BrushKey];

        return App.Current.Resources[visual.StyleKey];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value;

    private static TorrentStatusVisual GetVisual(object value)
    {
        var state = value switch
        {
            TorrentRuntimeStatus runtime => runtime.State,
            TorrentLifecycleState lifecycleState => lifecycleState,
            string text => FromText(text),
            _ => TorrentLifecycleState.Unknown
        };

        return state switch
        {
            TorrentLifecycleState.Downloading => new("TorrentBadgeDownloadingStyle", "TorrentAccentBrush", "\uE74B"),
            TorrentLifecycleState.Seeding => new("TorrentBadgeCompletedStyle", "TorrentGreenBrush", "\uE73E"),
            TorrentLifecycleState.Paused or TorrentLifecycleState.Stopped => new("TorrentBadgePausedStyle", "TorrentOrangeBrush", "\uE769"),
            TorrentLifecycleState.Checking or TorrentLifecycleState.Moving or TorrentLifecycleState.FetchingMetadata or TorrentLifecycleState.Stalled => new("TorrentBadgeCheckingStyle", "TorrentPurpleBrush", "\uE9D9"),
            TorrentLifecycleState.Error => new("TorrentBadgeErrorStyle", "TorrentRedBrush", "\uE783"),
            _ => new("TorrentBadgeNeutralStyle", "TorrentNeutralStatusBrush", "\uE946")
        };
    }

    private static TorrentLifecycleState FromText(string text)
    {
        if (text.Contains("скачив", StringComparison.OrdinalIgnoreCase))
            return TorrentLifecycleState.Downloading;
        if (text.Contains("разда", StringComparison.OrdinalIgnoreCase) || text.Contains("заверш", StringComparison.OrdinalIgnoreCase))
            return TorrentLifecycleState.Seeding;
        if (text.Contains("пауз", StringComparison.OrdinalIgnoreCase) || text.Contains("останов", StringComparison.OrdinalIgnoreCase))
            return TorrentLifecycleState.Paused;
        if (text.Contains("провер", StringComparison.OrdinalIgnoreCase) || text.Contains("метадан", StringComparison.OrdinalIgnoreCase))
            return TorrentLifecycleState.Checking;
        if (text.Contains("ошиб", StringComparison.OrdinalIgnoreCase))
            return TorrentLifecycleState.Error;

        return TorrentLifecycleState.Unknown;
    }

    private sealed record TorrentStatusVisual(string StyleKey, string BrushKey, string Glyph);
}
