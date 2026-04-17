using Microsoft.UI.Windowing;
using NiTorrent.Presentation.Features.Shell;
using WinUIEx;

namespace NiTorrent.App.Views;

public sealed partial class MainWindow : WindowEx
{
    public MainViewModel ViewModel { get; }
    public MainWindow()
    {
        ViewModel = App.GetService<MainViewModel>();

        this.InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        var navService = App.GetService<IJsonNavigationService>() as JsonNavigationService;
        if (navService != null)
        {
            navService.Initialize(NavView, NavFrame, NavigationPageMappings.PageDictionary)
                .ConfigureDefaultPage(typeof(HomeLandingPage))
                .ConfigureSettingsPage(typeof(SettingsPage))
                .ConfigureJsonFile("Assets/NavViewMenu/AppData.json")
                .ConfigureTitleBar(AppTitleBar)
                .ConfigureBreadcrumbBar(BreadCrumbNav, BreadcrumbPageMappings.PageDictionary);
        }
    }

    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        await App.GetService<IThemeService>().SetElementThemeWithoutSaveAsync();
    }

    private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        AutoSuggestBoxHelper.OnITitleBarAutoSuggestBoxTextChangedEvent(sender, args, NavFrame);
    }

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        AutoSuggestBoxHelper.OnITitleBarAutoSuggestBoxQuerySubmittedEvent(sender, args, NavFrame);
    }

    public void OpenTorrentFileFromActivation(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        if (NavFrame.Content is not TorrentPage)
            NavFrame.Navigate(typeof(TorrentPage));

        if (NavFrame.Content is TorrentPage torrentPage)
            _ = torrentPage.ViewModel.AddTorrentFileAsync(filePath, CancellationToken.None);
    }
}

