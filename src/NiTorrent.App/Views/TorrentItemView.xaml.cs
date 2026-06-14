using Microsoft.UI.Xaml.Input;
using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.App.Views;

public sealed partial class TorrentItemView : UserControl
{
    public static readonly DependencyProperty TorrentProperty =
        DependencyProperty.Register(
            nameof(Torrent),
            typeof(TorrentItemViewModel),
            typeof(TorrentItemView),
            new PropertyMetadata(null));

    public event EventHandler<TorrentItemViewModel>? DetailsRequested;

    public TorrentItemViewModel Torrent
    {
        get => (TorrentItemViewModel)GetValue(TorrentProperty);
        set => SetValue(TorrentProperty, value);
    }

    public TorrentItemView()
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
