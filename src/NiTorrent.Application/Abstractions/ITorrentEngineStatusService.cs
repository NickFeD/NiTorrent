namespace NiTorrent.Application.Abstractions;

public interface ITorrentEngineStatusService
{
    event Action? Loaded;
    Task InitializeAsync(CancellationToken ct = default);
}
