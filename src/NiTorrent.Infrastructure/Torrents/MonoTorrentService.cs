using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Legacy compatibility facade kept only for consumers that still depend on ITorrentService.
/// New read/status/write paths should go through application services directly.
/// </summary>
public sealed class MonoTorrentService : ITorrentService, IDisposable
{
    private readonly ITorrentReadModelFeed _readFeed;
    private readonly ITorrentEngineStatusService _engineStatusService;
    private readonly ITorrentEngineMaintenanceService _maintenanceService;
    private readonly ITorrentWriteService _writeService;

    public MonoTorrentService(
        ITorrentReadModelFeed readFeed,
        ITorrentEngineStatusService engineStatusService,
        ITorrentEngineMaintenanceService maintenanceService,
        ITorrentWriteService writeService)
    {
        _readFeed = readFeed;
        _engineStatusService = engineStatusService;
        _maintenanceService = maintenanceService;
        _writeService = writeService;

        _engineStatusService.Ready += OnReady;
        _readFeed.Updated += OnUpdated;
    }

    public event Action? Loaded;
    public event Action<IReadOnlyList<TorrentSnapshot>>? UpdateTorrent;

    public Task InitializeAsync(CancellationToken ct = default)
        => _engineStatusService.InitializeAsync(ct);

    public IReadOnlyList<TorrentSnapshot> GetAll()
        => _readFeed.Current;

    public TorrentSnapshot? TryGet(TorrentId id)
        => _readFeed.Current.FirstOrDefault(x => x.Id == id);

    public Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
        => _writeService.GetPreviewAsync(source, ct);

    public Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default)
        => _writeService.AddAsync(request, ct);

    public Task StartAsync(TorrentId id, CancellationToken ct = default)
        => _writeService.StartAsync(id, ct);

    public Task PauseAsync(TorrentId id, CancellationToken ct = default)
        => _writeService.PauseAsync(id, ct);

    public Task StopAsync(TorrentId id, CancellationToken ct = default)
        => _writeService.StopAsync(id, ct);

    public Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => _writeService.RemoveAsync(id, deleteData, ct);

    public void PublishTorrentUpdates()
        => _readFeed.Refresh();

    public Task ApplySettingsAsync()
        => _writeService.ApplySettingsAsync(CancellationToken.None);

    public Task SaveAsync(CancellationToken ct = default)
        => _maintenanceService.SaveStateAsync(ct);

    public Task ShutdownAsync(CancellationToken ct = default)
        => _maintenanceService.ShutdownAsync(ct);

    private void OnReady()
        => Loaded?.Invoke();

    private void OnUpdated(IReadOnlyList<TorrentSnapshot> snapshots)
        => UpdateTorrent?.Invoke(snapshots);

    public void Dispose()
    {
        _engineStatusService.Ready -= OnReady;
        _readFeed.Updated -= OnUpdated;
    }
}
