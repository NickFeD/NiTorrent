using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Transition-only adapter that exposes the old engine-centric <see cref="ITorrentService"/>
/// behind the new engine ports from Phase 3.
/// Remove after application workflows stop depending on ITorrentService directly.
/// </summary>
public sealed class LegacyMonoTorrentEngineAdapter :
    ITorrentEngineGateway,
    ITorrentEngineLifecycle,
    ITorrentRuntimeFactsProvider,
    ITorrentEngineStateStore
{
    private readonly ITorrentService _torrentService;

    public LegacyMonoTorrentEngineAdapter(ITorrentService torrentService)
    {
        _torrentService = torrentService;
        _torrentService.Loaded += () => Loaded?.Invoke();
        _torrentService.UpdateTorrent += HandleSnapshotUpdate;
    }

    public event Action? Loaded;
    public event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;

    public bool IsReady { get; private set; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _torrentService.InitializeAsync(ct).ConfigureAwait(false);
        IsReady = true;
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await _torrentService.ShutdownAsync(ct).ConfigureAwait(false);
        IsReady = false;
    }

    public Task SaveAsync(CancellationToken ct = default)
        => _torrentService.SaveAsync(ct);

    public Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
        => _torrentService.GetPreviewAsync(source, ct);

    public Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default)
        => _torrentService.AddAsync(request, ct);

    public Task StartAsync(TorrentId id, CancellationToken ct = default)
        => _torrentService.StartAsync(id, ct);

    public Task PauseAsync(TorrentId id, CancellationToken ct = default)
        => _torrentService.PauseAsync(id, ct);

    public Task StopAsync(TorrentId id, CancellationToken ct = default)
        => _torrentService.StopAsync(id, ct);

    public Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => _torrentService.RemoveAsync(id, deleteData, ct);

    public void PublishUpdates()
        => _torrentService.PublishTorrentUpdates();

    public IReadOnlyList<TorrentRuntimeFact> GetAll()
        => _torrentService.GetAll().Select(Map).ToList();

    public TorrentRuntimeFact? TryGet(TorrentId id)
    {
        var snapshot = _torrentService.TryGet(id);
        return snapshot is null ? null : Map(snapshot);
    }

    private void HandleSnapshotUpdate(IReadOnlyList<TorrentSnapshot> snapshots)
        => RuntimeFactsUpdated?.Invoke(snapshots.Select(Map).ToList());

    private static TorrentRuntimeFact Map(TorrentSnapshot snapshot)
        => new(
            snapshot.Id,
            new TorrentKey(snapshot.Key),
            snapshot.Name,
            snapshot.SavePath,
            new TorrentRuntimeState(
                TorrentLifecycleStateMapper.FromPhase(snapshot.Status.Phase),
                snapshot.Status.IsComplete,
                snapshot.Status.Progress,
                snapshot.Status.DownloadRateBytesPerSecond,
                snapshot.Status.UploadRateBytesPerSecond,
                snapshot.Status.Error,
                DateTimeOffset.UtcNow));
}
