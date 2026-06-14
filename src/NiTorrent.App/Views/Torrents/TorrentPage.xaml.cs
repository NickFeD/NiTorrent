using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using NiTorrent.Presentation.Features.Torrents;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NiTorrent.App.Views.Torrents;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class TorrentPage : Page
{
    private const double MediumLayoutWidth = 1280;
    private const double NarrowLayoutWidth = 980;
    private const double CompactSummaryWidth = 1120;
    private const double CompactHeaderWidth = 1180;
    private const double WideDetailsWidth = 420;
    private const double MediumDetailsWidth = 340;
    private const double DetailsMinWidth = 320;
    private const double WideListMinWidth = 560;
    private const double MediumListMinWidth = 460;
    private const double NarrowListMinWidth = 320;

    private TorrentPageLayoutMode _layoutMode = TorrentPageLayoutMode.Unset;
    private bool _isSummaryCompact;
    private bool _isHeaderCompact;

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
        => ViewModel.TorrentLoading(CancellationToken.None);
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

    private void TorrentCard_DetailsRequested(object? sender, TorrentItemViewModel torrent)
    {
        Frame?.Navigate(typeof(TorrentDetailsPage), torrent.Id.ToString());
    }

    private void TorrentDetailsSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (TorrentDetailsPanel.Visibility != Visibility.Visible)
            return;

        var totalResizableWidth = TorrentListColumn.ActualWidth + TorrentDetailsColumn.ActualWidth;
        var maxDetailsWidth = Math.Max(TorrentDetailsColumn.MinWidth, totalResizableWidth - TorrentListColumn.MinWidth);
        var requestedDetailsWidth = TorrentDetailsColumn.ActualWidth - e.HorizontalChange;
        var detailsWidth = Math.Clamp(requestedDetailsWidth, TorrentDetailsColumn.MinWidth, maxDetailsWidth);

        TorrentDetailsColumn.Width = new GridLength(detailsWidth);
        TorrentListColumn.Width = new GridLength(1, GridUnitType.Star);
    }

    private void TorrentSummarySplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var requestedSummaryHeight = TorrentSummaryRow.ActualHeight + e.VerticalChange;
        var summaryHeight = Math.Clamp(requestedSummaryHeight, TorrentSummaryRow.MinHeight, TorrentSummaryRow.MaxHeight);

        TorrentSummaryRow.Height = new GridLength(summaryHeight);
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyAdaptiveLayout(e.NewSize.Width);
    }

    private void ApplyAdaptiveLayout(double width)
    {
        var mode = width < NarrowLayoutWidth
            ? TorrentPageLayoutMode.Narrow
            : width < MediumLayoutWidth
                ? TorrentPageLayoutMode.Medium
                : TorrentPageLayoutMode.Wide;

        if (_layoutMode != mode)
        {
            ApplyMainLayoutMode(mode);
            _layoutMode = mode;
        }

        ApplySummaryLayout(width < CompactSummaryWidth);
        ApplyHeaderLayout(width < CompactHeaderWidth);
    }

    private void ApplyMainLayoutMode(TorrentPageLayoutMode mode)
    {
        switch (mode)
        {
            case TorrentPageLayoutMode.Narrow:
                TorrentListColumn.MinWidth = NarrowListMinWidth;
                TorrentListColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsSplitterColumn.Width = new GridLength(0);
                TorrentDetailsColumn.MinWidth = 0;
                TorrentDetailsColumn.Width = new GridLength(0);
                TorrentDetailsSplitter.Visibility = Visibility.Collapsed;
                TorrentDetailsPanel.Visibility = Visibility.Collapsed;
                TorrentListPanel.Margin = new Thickness(0);
                break;

            case TorrentPageLayoutMode.Medium:
                TorrentListColumn.MinWidth = MediumListMinWidth;
                TorrentListColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsSplitterColumn.Width = new GridLength(12);
                TorrentDetailsColumn.MinWidth = DetailsMinWidth;
                TorrentDetailsColumn.Width = new GridLength(MediumDetailsWidth);
                TorrentDetailsSplitter.Visibility = Visibility.Visible;
                TorrentDetailsPanel.Visibility = Visibility.Visible;
                TorrentListPanel.Margin = new Thickness(0, 0, 8, 0);
                break;

            case TorrentPageLayoutMode.Wide:
                TorrentListColumn.MinWidth = WideListMinWidth;
                TorrentListColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsSplitterColumn.Width = new GridLength(12);
                TorrentDetailsColumn.MinWidth = DetailsMinWidth;
                TorrentDetailsColumn.Width = new GridLength(WideDetailsWidth);
                TorrentDetailsSplitter.Visibility = Visibility.Visible;
                TorrentDetailsPanel.Visibility = Visibility.Visible;
                TorrentListPanel.Margin = new Thickness(0, 0, 8, 0);
                break;
        }
    }

    private void ApplySummaryLayout(bool isCompact)
    {
        if (_isSummaryCompact == isCompact)
            return;

        _isSummaryCompact = isCompact;

        if (isCompact)
        {
            TorrentSummaryRow.MinHeight = 232;
            TorrentSummaryRow.MaxHeight = 284;
            TorrentSummaryRow.Height = new GridLength(Math.Clamp(TorrentSummaryRow.ActualHeight, 232, 284));
            SummaryGrid.RowSpacing = 12;
            SummaryCardsSecondRow.Height = new GridLength(1, GridUnitType.Star);

            SummaryColumn0.Width = new GridLength(1, GridUnitType.Star);
            SummaryColumn1.Width = new GridLength(1, GridUnitType.Star);
            SummaryColumn2.Width = new GridLength(0);
            SummaryColumn3.Width = new GridLength(0);

            Grid.SetRow(ActiveSummaryCard, 0);
            Grid.SetColumn(ActiveSummaryCard, 0);
            Grid.SetRow(PausedSummaryCard, 0);
            Grid.SetColumn(PausedSummaryCard, 1);
            Grid.SetRow(CompletedSummaryCard, 1);
            Grid.SetColumn(CompletedSummaryCard, 0);
            Grid.SetRow(SpeedSummaryCard, 1);
            Grid.SetColumn(SpeedSummaryCard, 1);
        }
        else
        {
            TorrentSummaryRow.MinHeight = 118;
            TorrentSummaryRow.MaxHeight = 168;
            TorrentSummaryRow.Height = new GridLength(Math.Clamp(TorrentSummaryRow.ActualHeight, 118, 168));
            SummaryGrid.RowSpacing = 0;
            SummaryCardsSecondRow.Height = new GridLength(0);

            SummaryColumn0.Width = new GridLength(1, GridUnitType.Star);
            SummaryColumn1.Width = new GridLength(1, GridUnitType.Star);
            SummaryColumn2.Width = new GridLength(1, GridUnitType.Star);
            SummaryColumn3.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetRow(ActiveSummaryCard, 0);
            Grid.SetColumn(ActiveSummaryCard, 0);
            Grid.SetRow(PausedSummaryCard, 0);
            Grid.SetColumn(PausedSummaryCard, 1);
            Grid.SetRow(CompletedSummaryCard, 0);
            Grid.SetColumn(CompletedSummaryCard, 2);
            Grid.SetRow(SpeedSummaryCard, 0);
            Grid.SetColumn(SpeedSummaryCard, 3);
        }
    }

    private void ApplyHeaderLayout(bool isCompact)
    {
        if (_isHeaderCompact == isCompact)
            return;

        _isHeaderCompact = isCompact;

        if (isCompact)
        {
            Grid.SetRow(ToolbarPanel, 1);
            Grid.SetColumn(ToolbarPanel, 0);
            Grid.SetColumnSpan(ToolbarPanel, 2);
            ToolbarPanel.HorizontalAlignment = HorizontalAlignment.Left;
            ToolbarPanel.Margin = new Thickness(0, 12, 0, 0);
        }
        else
        {
            Grid.SetRow(ToolbarPanel, 0);
            Grid.SetColumn(ToolbarPanel, 1);
            Grid.SetColumnSpan(ToolbarPanel, 1);
            ToolbarPanel.HorizontalAlignment = HorizontalAlignment.Right;
            ToolbarPanel.Margin = new Thickness(0);
        }
    }

    private enum TorrentPageLayoutMode
    {
        Unset,
        Wide,
        Medium,
        Narrow
    }
}
