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

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            //if (e.Parameter is string raw && Guid.TryParse(raw, out var guid))
            //    await ViewModel.LoadAsync(new TorrentId(guid));
            //else if (e.Parameter is TorrentId torrentId)
            //    await ViewModel.LoadAsync(torrentId);

            //ViewModel.Activate();
        }
        catch
        {
            if (Frame?.CanGoBack == true)
                Frame.GoBack();
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.Deactivate();
        base.OnNavigatedFrom(e);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
            Frame.GoBack();
    }
}
