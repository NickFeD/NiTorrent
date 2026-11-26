using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Torrents.Contracts;

/// <summary>Detached product data. No live repository objects cross this interface.</summary>
public sealed record TorrentListItem(Guid Id, string InfoHash, string Name, string SavePath,
    long Size, TorrentDownloadStatus Status);

public sealed record BulkTorrentOutcome(int Applied, IReadOnlyList<Guid> Failed);

public interface ITorrentsModule
{
    event Action? EngineStateChanged;
    bool IsEngineReady { get; }
    string? EngineError { get; }
    Task<IReadOnlyList<TorrentListItem>> GetAllAsync(CancellationToken ct = default);
    Task<TorrentPreview> PreviewAsync(TorrentSource source, CancellationToken ct = default);
    Task<TorrentListItem> AddAsync(TorrentSource source, string directory,
        IReadOnlyList<TorrentFileEntry> files, CancellationToken ct = default);
    Task StartAsync(Guid id, CancellationToken ct = default);
    Task PauseAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, bool deleteFiles, CancellationToken ct = default);
    Task<BulkTorrentOutcome> SetAllAsync(bool running, CancellationToken ct = default);
    void Retry(Guid id);
}

/// <summary>Host-only admission control; stopping observation never stops the session.</summary>
public interface ITorrentsLifetime
{
    void RequestStop();
}
