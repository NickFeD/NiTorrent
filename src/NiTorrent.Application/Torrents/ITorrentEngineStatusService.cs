namespace NiTorrent.Application.Torrents;

public interface ITorrentEngineStatusService
{
    event Action? Ready;
    bool IsReady { get; }
    Task InitializeAsync(CancellationToken ct = default);
}
