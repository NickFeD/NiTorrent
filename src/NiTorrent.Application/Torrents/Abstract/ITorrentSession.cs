using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Abstract;

public enum TorrentPreparationState { Waiting, Preparing, Ready, Deleting, Failed }

public sealed record TorrentPreparation(TorrentPreparationState State, string? Error = null);

/// <summary>Application-owned session. Its lifetime is independent of windows and subscribers.</summary>
public interface ITorrentSession
{
    event Action? EngineStateChanged;
    bool IsEngineReady { get; }
    string? EngineError { get; }
    Task WaitForEngineAsync(CancellationToken ct);
    Task<T> UseEngineAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);
    Task AddAsync(TorrentDownload item, TorrentSource source, CancellationToken ct);
    Task SetStateAsync(Guid id, TorrentDownloadStatus state, CancellationToken ct);
    Task DeleteAsync(Guid id, bool deleteFiles, CancellationToken ct);
    void Schedule(Guid id, bool priority = true);
    void Retry(Guid id);
    IReadOnlyDictionary<Guid, TorrentPreparation> GetPreparations();
    void RequestStop();
}
