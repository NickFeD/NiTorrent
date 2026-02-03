using NiTorrent.Presentation.Features.Settings;

namespace NiTorrent.App.Views;

public sealed partial class AppUpdateSettingPage : Page
{
    public AppUpdateSettingViewModel ViewModel { get; }

    public AppUpdateSettingPage()
    {
        this.InitializeComponent();
        ViewModel = App.GetService<AppUpdateSettingViewModel>();
        DataContext = ViewModel;
    }
}


