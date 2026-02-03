using NiTorrent.Presentation.Features.Settings;

namespace NiTorrent.App.Views;

public sealed partial class AboutUsSettingPage : Page
{
    public AboutUsSettingViewModel ViewModel { get; }

    public AboutUsSettingPage()
    {
        ViewModel = App.GetService<AboutUsSettingViewModel>();
        this.InitializeComponent();
    }
}


