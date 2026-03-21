using NiTorrent.Presentation.Features.Torrents;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NiTorrent.App.Views;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class TorrentPage : Page
{
    public TorrentViewModel ViewModel { get; }

    public TorrentPage()
    {
        ViewModel = App.GetService<TorrentViewModel>();
        InitializeComponent();
    }


    private void AddMagnet_Click(object sender, RoutedEventArgs e)
    {
        MagnetInput.Text = "";
        MagnetTip.IsOpen = true;
    }

    private void CancelMagnet_Click(object sender, RoutedEventArgs e)
    {
        MagnetTip.IsOpen = false;
    }

    private async void ConfirmMagnet_Click(object sender, RoutedEventArgs e)
    {
        var magnet = MagnetInput.Text;

        if (!string.IsNullOrWhiteSpace(magnet))
        {
            // вызываем команду ViewModel
            await ViewModel.AddMagnet(magnet);
        }

        MagnetTip.IsOpen = false;
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.RefreshCommands();
    }

    private void TorrentList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.SelectedTorrent is null)
            return;

        Frame?.Navigate(typeof(TorrentDetailsPage), ViewModel.SelectedTorrent.Id.ToString());
    }

}


