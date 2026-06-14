using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.App.Views.Torrents;

public sealed partial class TorrentDetailsPanel : UserControl
{
    public static readonly DependencyProperty TorrentProperty =
        DependencyProperty.Register(
            nameof(Torrent),
            typeof(TorrentItemViewModel),
            typeof(TorrentDetailsPanel),
            new PropertyMetadata(null, OnTorrentChanged));

    public TorrentItemViewModel? Torrent
    {
        get => (TorrentItemViewModel?)GetValue(TorrentProperty);
        set => SetValue(TorrentProperty, value);
    }

    public TorrentDetailsPanel()
    {
        InitializeComponent();
        UpdateState();
    }

    private static void OnTorrentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TorrentDetailsPanel panel)
        {
            panel.UpdateState();
        }
    }

    private void UpdateState()
    {
        var hasTorrent = Torrent is not null;
        EmptyState.Visibility = hasTorrent ? Visibility.Collapsed : Visibility.Visible;
        DetailsHost.Visibility = hasTorrent ? Visibility.Visible : Visibility.Collapsed;
    }
}
