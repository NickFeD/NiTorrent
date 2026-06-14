using NiTorrent.Presentation.Features.Torrents;

namespace NiTorrent.App.Views.Torrents;

public sealed partial class TorrentItemView : UserControl
{
    private const double CompactWidth = 820;
    private bool _isCompact;

    public event EventHandler<TorrentItemViewModel>? DetailsRequested;

    public TorrentItemViewModel? Torrent => DataContext as TorrentItemViewModel;

    public TorrentItemView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (Torrent is not { } torrent)
            return;

        DetailsRequested?.Invoke(this, torrent);
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyLayout(e.NewSize.Width < CompactWidth);
    }

    private void ApplyLayout(bool isCompact)
    {
        if (_isCompact == isCompact)
            return;

        _isCompact = isCompact;

        var secondaryVisibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        StateColumn.Width = isCompact ? new GridLength(0) : new GridLength(102);
        DownloadColumn.Width = isCompact ? new GridLength(0) : new GridLength(96);
        UploadColumn.Width = isCompact ? new GridLength(0) : new GridLength(96);
        SizeColumn.Width = isCompact ? new GridLength(74) : new GridLength(80);
        ProgressColumn.Width = isCompact ? new GridLength(82) : new GridLength(102);

        StateTextBlock.Visibility = secondaryVisibility;
        DownloadTextBlock.Visibility = secondaryVisibility;
        UploadTextBlock.Visibility = secondaryVisibility;
    }
}
