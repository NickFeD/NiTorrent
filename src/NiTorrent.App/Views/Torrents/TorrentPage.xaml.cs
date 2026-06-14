using Microsoft.UI.Input;
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
    private const double CompactTableWidth = 820;
    private const double HideTableHeaderWidth = 640;
    private const double WideDetailsWidth = 380;
    private const double DetailsMinWidth = 320;
    private const double WideListMinWidth = 560;
    private const double MediumListMinWidth = 460;
    private const double NarrowListMinWidth = 320;

    private TorrentPageLayoutMode _layoutMode = TorrentPageLayoutMode.Unset;
    private bool _isSummaryCompact;
    private bool _isHeaderCompact;
    private bool _isTableCompact;

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

        var totalResizableWidth = MainContentColumn.ActualWidth + TorrentDetailsColumn.ActualWidth;
        var maxDetailsWidth = Math.Max(TorrentDetailsColumn.MinWidth, totalResizableWidth - MainContentColumn.MinWidth);
        var requestedDetailsWidth = TorrentDetailsColumn.ActualWidth - e.HorizontalChange;
        var detailsWidth = Math.Clamp(requestedDetailsWidth, TorrentDetailsColumn.MinWidth, maxDetailsWidth);

        TorrentDetailsColumn.Width = new GridLength(detailsWidth);
        MainContentColumn.Width = new GridLength(1, GridUnitType.Star);
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
        ApplyTableLayout(width < CompactTableWidth, width < HideTableHeaderWidth);
    }

    private void ApplyMainLayoutMode(TorrentPageLayoutMode mode)
    {
        switch (mode)
        {
            case TorrentPageLayoutMode.Narrow:
                MainContentColumn.MinWidth = NarrowListMinWidth;
                MainContentColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsOuterRow.Height = new GridLength(0);
                TorrentContentGrid.ColumnSpacing = 0;
                TorrentContentGrid.RowSpacing = 0;
                TorrentListRow.Height = new GridLength(1, GridUnitType.Star);
                TorrentListColumn.MinWidth = NarrowListMinWidth;
                TorrentListColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsSplitterColumn.Width = new GridLength(0);
                TorrentDetailsColumn.MinWidth = 0;
                TorrentDetailsColumn.Width = new GridLength(0);
                Grid.SetRow(TorrentListPanel, 0);
                Grid.SetColumn(TorrentListPanel, 0);
                Grid.SetColumnSpan(TorrentListPanel, 1);
                Grid.SetRow(TorrentDetailsSplitter, 4);
                Grid.SetRowSpan(TorrentDetailsSplitter, 1);
                Grid.SetColumn(TorrentDetailsSplitter, 0);
                Grid.SetRow(TorrentDetailsPanel, 4);
                Grid.SetRowSpan(TorrentDetailsPanel, 1);
                Grid.SetColumn(TorrentDetailsPanel, 0);
                Grid.SetColumnSpan(TorrentDetailsPanel, 1);
                TorrentDetailsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                TorrentDetailsSplitter.Visibility = Visibility.Collapsed;
                TorrentDetailsPanel.Visibility = Visibility.Collapsed;
                TorrentListPanel.Margin = new Thickness(0);
                break;

            case TorrentPageLayoutMode.Medium:
                MainContentColumn.MinWidth = MediumListMinWidth;
                MainContentColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsOuterRow.Height = new GridLength(0);
                TorrentContentGrid.ColumnSpacing = 0;
                TorrentContentGrid.RowSpacing = 0;
                TorrentListRow.Height = new GridLength(1, GridUnitType.Star);
                TorrentListColumn.MinWidth = MediumListMinWidth;
                TorrentListColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsSplitterColumn.Width = new GridLength(0);
                TorrentDetailsColumn.MinWidth = 0;
                TorrentDetailsColumn.Width = new GridLength(0);
                Grid.SetRow(TorrentListPanel, 0);
                Grid.SetColumn(TorrentListPanel, 0);
                Grid.SetColumnSpan(TorrentListPanel, 1);
                Grid.SetRow(TorrentDetailsSplitter, 4);
                Grid.SetRowSpan(TorrentDetailsSplitter, 1);
                Grid.SetColumn(TorrentDetailsSplitter, 0);
                Grid.SetRow(TorrentDetailsPanel, 4);
                Grid.SetRowSpan(TorrentDetailsPanel, 1);
                Grid.SetColumn(TorrentDetailsPanel, 0);
                Grid.SetColumnSpan(TorrentDetailsPanel, 1);
                TorrentDetailsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                TorrentDetailsSplitter.Visibility = Visibility.Collapsed;
                TorrentDetailsPanel.Visibility = Visibility.Collapsed;
                TorrentListPanel.Margin = new Thickness(0);
                break;

            case TorrentPageLayoutMode.Wide:
                MainContentColumn.MinWidth = WideListMinWidth;
                MainContentColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsOuterRow.Height = new GridLength(0);
                TorrentContentGrid.ColumnSpacing = 0;
                TorrentContentGrid.RowSpacing = 0;
                TorrentListRow.Height = new GridLength(1, GridUnitType.Star);
                TorrentListColumn.MinWidth = WideListMinWidth;
                TorrentListColumn.Width = new GridLength(1, GridUnitType.Star);
                TorrentDetailsSplitterColumn.Width = new GridLength(10);
                TorrentDetailsColumn.MinWidth = DetailsMinWidth;
                TorrentDetailsColumn.Width = new GridLength(WideDetailsWidth);
                Grid.SetRow(TorrentListPanel, 0);
                Grid.SetColumn(TorrentListPanel, 0);
                Grid.SetColumnSpan(TorrentListPanel, 1);
                Grid.SetRow(TorrentDetailsSplitter, 0);
                Grid.SetRowSpan(TorrentDetailsSplitter, 4);
                Grid.SetColumn(TorrentDetailsSplitter, 1);
                Grid.SetRow(TorrentDetailsPanel, 0);
                Grid.SetRowSpan(TorrentDetailsPanel, 4);
                Grid.SetColumn(TorrentDetailsPanel, 2);
                Grid.SetColumnSpan(TorrentDetailsPanel, 1);
                TorrentDetailsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                TorrentDetailsSplitter.Visibility = Visibility.Visible;
                TorrentDetailsPanel.Visibility = Visibility.Visible;
                TorrentListPanel.Margin = new Thickness(0);
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
            TorrentSummaryRow.MinHeight = 180;
            TorrentSummaryRow.MaxHeight = 220;
            TorrentSummaryRow.Height = new GridLength(Math.Clamp(TorrentSummaryRow.ActualHeight, 180, 220));
            SummaryGrid.RowSpacing = 10;
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
            TorrentSummaryRow.MinHeight = 86;
            TorrentSummaryRow.MaxHeight = 112;
            TorrentSummaryRow.Height = new GridLength(Math.Clamp(TorrentSummaryRow.ActualHeight, 86, 112));
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
            Grid.SetRow(ToolbarSurface, 1);
            Grid.SetColumn(ToolbarSurface, 0);
            Grid.SetColumnSpan(ToolbarSurface, 2);
            ToolbarSurface.HorizontalAlignment = HorizontalAlignment.Left;
            ToolbarSurface.Margin = new Thickness(0, 10, 0, 0);
        }
        else
        {
            Grid.SetRow(ToolbarSurface, 0);
            Grid.SetColumn(ToolbarSurface, 1);
            Grid.SetColumnSpan(ToolbarSurface, 1);
            ToolbarSurface.HorizontalAlignment = HorizontalAlignment.Right;
            ToolbarSurface.Margin = new Thickness(0);
        }
    }

    private void ApplyTableLayout(bool isCompact, bool hideHeader)
    {
        if (_isTableCompact == isCompact)
        {
            TorrentListHeaderRow.Height = hideHeader ? new GridLength(0) : new GridLength(38);
            TorrentListHeader.Visibility = hideHeader ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        _isTableCompact = isCompact;

        var secondaryVisibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        var secondaryWidth = isCompact ? 0 : 96;

        HeaderDownloadColumn.Width = new GridLength(secondaryWidth);
        HeaderUploadColumn.Width = new GridLength(secondaryWidth);
        HeaderDownloadPanel.Visibility = secondaryVisibility;
        HeaderUploadPanel.Visibility = secondaryVisibility;

        HeaderStateColumn.Width = isCompact ? new GridLength(0) : new GridLength(102);
        HeaderStateText.Visibility = secondaryVisibility;

        HeaderSizeColumn.Width = isCompact ? new GridLength(74) : new GridLength(80);
        HeaderProgressColumn.Width = isCompact ? new GridLength(82) : new GridLength(102);
        TorrentListHeaderRow.Height = hideHeader ? new GridLength(0) : new GridLength(38);
        TorrentListHeader.Visibility = hideHeader ? Visibility.Collapsed : Visibility.Visible;
    }

    private void TorrentDetailsSplitter_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }

    private void TorrentDetailsSplitter_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = null;
    }

    private void TorrentSummarySplitter_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    }

    private void TorrentSummarySplitter_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = null;
    }

    private enum TorrentPageLayoutMode
    {
        Unset,
        Wide,
        Medium,
        Narrow
    }
}
