using Microsoft.UI.Xaml.Input;
using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.App.Views.Torrents;

public sealed partial class TorrentCardView : UserControl
{
    public static readonly DependencyProperty TorrentProperty =
        DependencyProperty.Register(
            nameof(Torrent),
            typeof(TorrentItemViewModel),
            typeof(TorrentCardView),
            new PropertyMetadata(null));

    public event EventHandler<TorrentItemViewModel>? DetailsRequested;

    public TorrentItemViewModel? Torrent
    {
        get => (TorrentItemViewModel?)GetValue(TorrentProperty);
        set => SetValue(TorrentProperty, value);
    }

    public TorrentCardView()
    {
        InitializeComponent();
    }

    private void TorrentItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void TorrentDetails_Click(object sender, RoutedEventArgs e)
    {
        var torrent = Torrent;
        if (torrent is null)
            return;

        DetailsRequested?.Invoke(this, torrent);
    }
}
