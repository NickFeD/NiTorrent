namespace NiTorrent.App.Views;

public sealed partial class SettingsPage : Page
{
    public IJsonNavigationService NavService { get; }

    public SettingsPage()
    {
        NavService = App.GetService<IJsonNavigationService>();
        InitializeComponent();
    }
}
