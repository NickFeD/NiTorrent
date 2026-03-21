using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Transition-only adapter that exposes legacy ITorrentService through the new engine ports.
/// Remove when MonoTorrent integration is split into dedicated engine components.
/// </summary>
public sealed class LegacyMonoTorrentEngineAdapter : ITorrentEngineGateway, ITorrentEngineLifecycle, ITorrentRuntimeFactsProvider, ITorrentEngineStateStore
{
    private readonly ITorrentService _legacy;

    public event Action<IReadOnlyList<TorrentRuntimeFact>>? RuntimeFactsUpdated;

    public LegacyMonoTorrentEngineAdapter(ITorrentService legacy)
    {
        _legacy = legacy;
        _legacy.UpdateTorrent += HandleLegacyUpdated;
    }

    public Task InitializeAsync(CancellationToken ct = default) => _legacy.InitializeAsync(ct);
    public Task ShutdownAsync(CancellationToken ct = default) => _legacy.ShutdownAsync(ct);
    public Task SaveAsync(CancellationToken ct = default) => _legacy.SaveAsync(ct);

    public Task StartAsync(TorrentId id, CancellationToken ct = default) => _legacy.StartAsync(id, ct);
    public Task PauseAsync(TorrentId id, CancellationToken ct = default) => _legacy.PauseAsync(id, ct);
    public Task StopAsync(TorrentId id, CancellationToken ct = default) => _legacy.StopAsync(id, ct);
    public Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default) => _legacy.RemoveAsync(id, deleteData, ct);

    public IReadOnlyList<TorrentRuntimeFact> GetAll() => _legacy.GetAll().Select(MapSnapshot).ToList();

    private void HandleLegacyUpdated(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        RuntimeFactsUpdated?.Invoke(snapshots.Select(MapSnapshot).ToList());
    }

    private static TorrentRuntimeFact MapSnapshot(TorrentSnapshot snapshot)
    {
        var runtime = new TorrentRuntimeState(
            TorrentLifecycleStateMapper.FromPhase(snapshot.Status.Phase),
            snapshot.Status.IsComplete,
            snapshot.Status.Progress,
            snapshot.Status.DownloadRateBytesPerSecond,
            snapshot.Status.UploadRateBytesPerSecond,
            snapshot.Status.Error,
            snapshot.Status.Source == TorrentSnapshotSource.Live);

        return new TorrentRuntimeFact(
            snapshot.Id,
            string.IsNullOrWhiteSpace(snapshot.Key) ? TorrentKey.Empty : new TorrentKey(snapshot.Key),
            snapshot.Name,
            snapshot.Size,
            snapshot.SavePath,
            runtime);
    }
}
