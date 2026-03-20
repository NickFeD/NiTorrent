namespace NiTorrent.App.Services.AppLifecycle;

public interface IAppShutdownCoordinator
{
    Task ShutdownAsync(Func<Task> stopHostAsync, Action exitApplication);
}
