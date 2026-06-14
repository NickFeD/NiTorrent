using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace NiTorrent.App.Views.Torrents;

public sealed partial class TorrentSummaryCard : UserControl
{
    private bool _isInitialized;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(TorrentSummaryCard),
            new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(TorrentSummaryCard),
            new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle),
            typeof(string),
            typeof(TorrentSummaryCard),
            new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(
            nameof(IconGlyph),
            typeof(string),
            typeof(TorrentSummaryCard),
            new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(
            nameof(AccentBrush),
            typeof(Brush),
            typeof(TorrentSummaryCard),
            new PropertyMetadata(new SolidColorBrush(Colors.DodgerBlue), OnVisualPropertyChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public TorrentSummaryCard()
    {
        InitializeComponent();
        _isInitialized = true;
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TorrentSummaryCard { _isInitialized: true } card)
        {
            card.Bindings.Update();
        }
    }
}
