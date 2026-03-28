namespace NiTorrent.App.Views;

public sealed partial class HomeLandingPage : Page
{
    public IJsonNavigationService NavService { get; }

    public HomeLandingPage()
    {
        NavService = App.GetService<IJsonNavigationService>();
        InitializeComponent();
    }
}
