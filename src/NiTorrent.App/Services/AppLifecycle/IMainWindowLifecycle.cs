using Microsoft.UI.Xaml;

namespace NiTorrent.App.Services.AppLifecycle;

public interface IMainWindowLifecycle : IDisposable
{
    event Func<Task>? CloseRequested;
    event Func<Task>? ExplicitExitRequested;

    Window CreateAndInitialize();
    void Activate();
    Task ShowAsync();
    Task HideToTrayAsync();
    Task CloseForShutdownAsync();
}
