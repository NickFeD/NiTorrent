using NiTorrent.Presentation.Features.Settings;

namespace NiTorrent.App.Views;

public sealed partial class TorrentSettingPage : Page
{
    public TorrentSettingsViewModel ViewModel { get; }

    public TorrentSettingPage()
    {
        ViewModel = App.GetService<TorrentSettingsViewModel>();
        DataContext = ViewModel;

        InitializeComponent();
        Loaded += OnLoaded;
        Bindings.Update();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.EnsureLoadedAsync();
    }
}
