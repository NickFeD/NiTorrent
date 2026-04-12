using Microsoft.UI.Xaml.Input;
using NiTorrent.Presentation.Features.Torrents;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NiTorrent.App.Views;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class TorrentPage : Page
{
    private TorrentItemViewModel? _contextMenuTorrent;

    public TorrentViewModel ViewModel { get; }

    public TorrentPage()
    {
        ViewModel = App.GetService<TorrentViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        //_ = ViewModel.TorrentLoading(CancellationToken.None);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        //ViewModel.TorrentUnloaded();
        base.OnNavigatedFrom(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => _ = ViewModel.TorrentLoading(CancellationToken.None);
    private void OnUnloaded(object sender, RoutedEventArgs e)
        => ViewModel.TorrentUnloaded();

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
            await ViewModel.AddMagnet(magnet, CancellationToken.None);
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

    private void TorrentItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TorrentItemViewModel torrent)
            return;

        _contextMenuTorrent = torrent;

        // Keep current selection unchanged when opening context menu by right click.
        e.Handled = true;
    }

    private void TorrentDetailsContext_Click(object sender, RoutedEventArgs e)
    {
        var torrent = _contextMenuTorrent ?? ViewModel.SelectedTorrent;
        if (torrent is null)
            return;

        Frame?.Navigate(typeof(TorrentDetailsPage), torrent.Id.ToString());
    }
}
