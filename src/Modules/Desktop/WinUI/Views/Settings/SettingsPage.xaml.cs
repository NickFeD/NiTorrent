namespace NiTorrent.App.Views.Settings;

public sealed partial class SettingsPage : Page
{
    public IJsonNavigationService NavService { get; }

    public SettingsPage()
    {
        NavService = DesktopServices.GetService<IJsonNavigationService>();
        InitializeComponent();
    }
}
