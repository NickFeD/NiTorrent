namespace NiTorrent.Application.Abstractions;

public interface ITorrentEngineLifecycle
{
    Task InitializeAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}
