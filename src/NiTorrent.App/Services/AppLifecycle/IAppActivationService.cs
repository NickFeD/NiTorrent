using Microsoft.Windows.AppLifecycle;

namespace NiTorrent.App.Services.AppLifecycle;

public interface IAppActivationService
{
    Task HandleAsync(AppActivationArguments args, Action showMainWindow, Action startBackgroundInitialization);
}
