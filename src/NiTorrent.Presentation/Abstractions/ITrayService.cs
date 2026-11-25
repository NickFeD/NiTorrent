namespace NiTorrent.Presentation.Abstractions;

public interface ITrayService : IDisposable
{
    event Action? OpenRequested;
    event Func<Task>? ExitRequested;
    event Action? RecoveryRequested;
    event Action<int>? BulkFailed;

    // Show must either register a usable icon or throw. Hide also cleans partial registrations.
    void Show();
    void Hide();
}
