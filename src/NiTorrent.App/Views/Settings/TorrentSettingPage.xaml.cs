using NiTorrent.Presentation.Features.Settings;

namespace NiTorrent.App.Views;

public sealed partial class TorrentSettingPage : Page
{
    public TorrentSettingsViewModel ViewModel { get; }

    public TorrentSettingPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<TorrentSettingsViewModel>();
        DataContext = ViewModel;
    }
}
