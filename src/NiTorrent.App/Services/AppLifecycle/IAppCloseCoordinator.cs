namespace NiTorrent.App.Services.AppLifecycle;

public interface IAppCloseCoordinator
{
    bool IsExitInProgress { get; }

    Task RequestCloseFromWindowAsync(Func<Task> exitAsync);
    Task RequestExplicitExitAsync(Func<Task> exitAsync);
}
