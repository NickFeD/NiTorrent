namespace NiTorrent.Application.Abstractions;

public enum WindowCloseAction
{
    ExitApplication = 0,
    MinimizeToTray = 1
}

public sealed record WindowCloseChoice(WindowCloseAction Action, bool RememberChoice);

public interface IDialogService
{
    Task ShowTextAsync(string title, string text, CancellationToken ct = default);
    Task<WindowCloseChoice?> ShowWindowCloseChoiceAsync(bool defaultMinimizeToTray, CancellationToken ct = default);
}
