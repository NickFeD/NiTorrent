namespace NiTorrent.App.Views;

public sealed partial class ThemeSettingPage : Page
{
    public IThemeService ThemeService { get; }
    public ThemeSettingPage()
    {
        ThemeService = App.GetService<IThemeService>();
        this.InitializeComponent();
    }
}


