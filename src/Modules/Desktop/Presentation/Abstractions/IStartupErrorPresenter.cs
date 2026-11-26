namespace NiTorrent.Presentation.Abstractions;

public interface IStartupErrorPresenter
{
    Task ShowAsync(string message, Func<Task> exit, Func<bool> isStopping);
    Task CloseAsync(CancellationToken ct);
}
