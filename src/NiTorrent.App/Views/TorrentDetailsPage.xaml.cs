using Microsoft.UI.Xaml.Navigation;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.App.Views;

public sealed partial class TorrentDetailsPage : Page
{
    public TorrentDetailsViewModel ViewModel { get; }

    public TorrentDetailsPage()
    {
        ViewModel = App.GetService<TorrentDetailsViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string raw && Guid.TryParse(raw, out var guid))
            ViewModel.Load(new TorrentId(guid));
        else if (e.Parameter is TorrentId torrentId)
            ViewModel.Load(torrentId);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
            Frame.GoBack();
    }
}
