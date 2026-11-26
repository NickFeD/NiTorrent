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
        Loaded += OnLoaded;
        UpdateState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DetailsSelectorBar.SelectedItem is null && DetailsSelectorBar.Items.Count > 0)
            DetailsSelectorBar.SelectedItem = DetailsSelectorBar.Items[0];

        UpdateSelectedTab();
    }

    private static void OnTorrentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TorrentDetailsPanel panel)
        {
            panel.Bindings.Update();
            panel.UpdateState();
        }
    }

    private void UpdateState()
    {
        var hasTorrent = Torrent is not null;
        EmptyState.Visibility = hasTorrent ? Visibility.Collapsed : Visibility.Visible;
        DetailsContent.Visibility = hasTorrent ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DetailsSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        UpdateSelectedTab();
    }

    private void UpdateSelectedTab()
    {
        var selectedIndex = DetailsSelectorBar.Items.IndexOf(DetailsSelectorBar.SelectedItem);

        OverviewPane.Visibility = selectedIndex <= 0 ? Visibility.Visible : Visibility.Collapsed;
        FilesPane.Visibility = selectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        PeersPane.Visibility = selectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        TrackersPane.Visibility = selectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }
}
