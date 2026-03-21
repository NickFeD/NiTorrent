namespace NiTorrent.Application.Abstractions;

public interface ITorrentEngineLifecycle
{
    event Action? Loaded;
    bool IsReady { get; }
    Task InitializeAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}
