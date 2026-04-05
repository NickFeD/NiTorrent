using NiTorrent.Presentation.Features.Settings;

namespace NiTorrent.App.Views;

public sealed partial class ThemeSettingPage : Page
{
    public ThemeSettingsViewModel ViewModel { get; }

    public ThemeSettingPage()
    {
        ViewModel = App.GetService<ThemeSettingsViewModel>();
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
