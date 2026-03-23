namespace NiTorrent.Application.Abstractions;

public interface ITorrentEngineStateStore
{
    Task SaveAsync(CancellationToken ct = default);
}
