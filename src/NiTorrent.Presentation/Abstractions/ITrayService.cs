namespace NiTorrent.Presentation.Abstractions;

public interface ITrayService : IDisposable
{
    event Action? OpenRequested;
    event Func<Task>? ExitRequested;

    void Initialize();
    void SetVisible(bool visible);
    void UpdateTotals(string totalDownload, string totalUpload);
}
