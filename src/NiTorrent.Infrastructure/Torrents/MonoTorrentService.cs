using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Settings;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class MonoTorrentService : ITorrentService
{
    private readonly ILogger<MonoTorrentService> _logger;
    private readonly IAppStorageService _storage;
    private readonly ITorrentPreferences _prefs;
    private readonly IDialogService _dialogs;

    private readonly TorrentCatalogStore _catalogStore;
    private readonly TorrentCommandQueue _commandQueue = new();

    // Single gate to guarantee consistency across:
    // engine lifecycle, _byId, and catalog operations.
    private readonly SemaphoreSlim _opGate = new(1, 1);

    // Save gate: prevents concurrent engine SaveState.
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    // Lifecycle gate: serializes mutations and shutdown/save to reduce races on exit.
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private readonly string _cacheDir;
    private readonly string _stateFilePath;

    private ClientEngine? _engine;
    private bool _engineReady;

    // Initialization (once)
    private Task? _initTask;

    // TorrentId -> TorrentManager
    private readonly Dictionary<TorrentId, TorrentManager> _byId = new();

    public event Action? Loaded;

    public event Action<IReadOnlyList<TorrentSnapshot>>? UpdateTorrent;

    public MonoTorrentService(
        ILogger<MonoTorrentService> logger,
        IAppStorageService storage,
        ITorrentPreferences prefs,
        IDialogService dialogs,
        TorrentCatalogStore catalogStore)
    {
        _logger = logger;
        _storage = storage;
        _prefs = prefs;
        _dialogs = dialogs;
        _catalogStore = catalogStore;

        _cacheDir = _storage.GetCachePath(@"Torrents\cache");
        _stateFilePath = _storage.GetLocalPath(@"Torrents\torrent_engine.dat");

        _storage.EnsureDirectory(_cacheDir);
        _storage.EnsureParentDirectory(_stateFilePath);
    }

    private ClientEngine Engine
        => _engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Initializing torrent service");

        FireAndForget(PublishCachedSnapshotsAsync(ct), "publish-cached");
        await EnsureStartedAsync(ct).ConfigureAwait(false);
    }

    private Task EnsureStartedAsync(CancellationToken ct = default)
    {
        // Fast path
        if (_initTask is not null) return _initTask;

        // Single assignment protected by _opGate
        return StartOnceAsync(ct);

        async Task StartOnceAsync(CancellationToken ct2)
        {
            Task initTask;

            await _opGate.WaitAsync(ct2).ConfigureAwait(false);
            try
            {
                _initTask ??= LoadEngineInternalAsync(ct2);
                initTask = _initTask;
            }
            finally
            {
                _opGate.Release();
            }

            try
            {
                await initTask.ConfigureAwait(false);
            }
            catch
            {
                await _opGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    if (ReferenceEquals(_initTask, initTask))
                        _initTask = null;
                }
                finally
                {
                    _opGate.Release();
                }

                throw;
            }
        }
    }

    private async Task LoadEngineInternalAsync(CancellationToken ct)
    {
        try
        {
            // Ensure catalog is loaded before we attach restored managers.
            await _catalogStore.EnsureLoadedAsync(ct).ConfigureAwait(false);

            await _opGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _engine = await CreateEngineWithRecoveryAsync(ct).ConfigureAwait(false);

                // Match restored managers to stable ids from catalog.
                var pendingRemovals = await _catalogStore.AttachRestoredManagersAsync(Engine, _byId, GetStableKey, ct).ConfigureAwait(false);

                _engineReady = true;

                _ = Task.Run(async () =>
                {
                    foreach (var pendingRemoval in pendingRemovals)
                    {
                        try
                        {
                            var mode = pendingRemoval.DeleteDownloadedData
                                ? RemoveMode.CacheDataAndDownloadedData
                                : RemoveMode.CacheDataOnly;

                            await Engine.RemoveAsync(pendingRemoval.Manager, mode).ConfigureAwait(false);
                            await _catalogStore.CompletePendingRemovalAsync(pendingRemoval.Key, ct).ConfigureAwait(false);
                            _logger.LogInformation("Removed pending cached torrent {TorrentKey} after engine restore", pendingRemoval.Key);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to remove pending cached torrent {TorrentKey}", pendingRemoval.Key);
                        }
                    }

                    await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
                }, ct);
            }
            finally
            {
                _opGate.Release();
            }

            // Apply queued user intent (outside _opGate: does I/O and async start/stop)
            await ApplyQueuedIntentAsync(ct).ConfigureAwait(false);

            // Auto-start torrents that should run
            FireAndForget(AutoStartFromCatalogAsync(ct), "auto-start");

            Loaded?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize torrent engine");
            await NotifyAsync("Ошибка запуска торрент-движка", $"Не удалось запустить торрент-движок.\n\n{ex.Message}").ConfigureAwait(false);
            throw;
        }
    }

    public IReadOnlyList<TorrentSnapshot> GetAll()
    {
        // We keep GetAll sync for UI convenience.
        // It reads shared state; ensure callers do not call concurrently with heavy operations,
        // OR you can change this method to async + _opGate for stricter guarantees.
        if (_engine is null)
        {
            // best-effort: if catalog isn't loaded yet, return empty quickly
            // (cached publish will update UI soon).
            try
            {
                // BuildCachedSnapshotsAsync is async; here we return empty for non-blocking sync call.
                return [];
            }
            catch
            {
                return [];
            }
        }

        return [.. _byId.Select(kv => BuildSnapshot(kv.Key, kv.Value, addedAtUtc: null))];
    }

    public TorrentSnapshot? TryGet(TorrentId id)
    {
        if (_byId.TryGetValue(id, out var m))
            return BuildSnapshot(id, m, addedAtUtc: null);

        // If engine isn't ready - try cached
        // Sync method: return null here; UI will have cached list from PublishCachedSnapshotsAsync.
        return null;
    }

    public async Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
    {
        if (source is TorrentSource.Magnet)
            await EnsureStartedAsync(ct).ConfigureAwait(false);

        var torrent = await ResolveTorrentAsync(source, ct).ConfigureAwait(false);

        var files = torrent.Files
            .Select(f => new TorrentFileEntry(f.Path, f.Length, true))
            .ToList();

        return new TorrentPreview(torrent.Name, torrent.Size, files);
    }

    public async Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            _logger.LogInformation("Adding torrent");
            var torrent = await ResolveTorrentAsync(request.Source, ct).ConfigureAwait(false);
            var manager = await Engine.AddAsync(torrent, request.SavePath).ConfigureAwait(false);

            if (request.SelectedFilePaths is { Count: > 0 })
            {
                var selected = new HashSet<string>(request.SelectedFilePaths, StringComparer.OrdinalIgnoreCase);
                var batch = new List<Task>(256);

                foreach (var file in manager.Files)
                {
                    if (selected.Contains(file.Path))
                        continue;

                    batch.Add(manager.SetFilePriorityAsync(file, Priority.DoNotDownload));
                    if (batch.Count >= 200)
                    {
                        await Task.WhenAll(batch).ConfigureAwait(false);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                    await Task.WhenAll(batch).ConfigureAwait(false);
            }

            var id = new TorrentId(Guid.NewGuid());
            var addedAt = DateTimeOffset.UtcNow;

            await _opGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _byId[id] = manager;
            }
            finally
            {
                _opGate.Release();
            }

            var snapshot = BuildSnapshot(id, manager, addedAtUtc: addedAt);
            await _catalogStore.UpsertFromSnapshotAsync(snapshot, ct).ConfigureAwait(false);
            _logger.LogInformation("Starting torrent {TorrentId}", id.Value);
            await _catalogStore.SetShouldRunAsync(id, true, ct).ConfigureAwait(false);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);

            await manager.StartAsync().ConfigureAwait(false);

            FireAndForget(SaveAsync(CancellationToken.None), "save-engine-state");
            return id;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StartAsync(TorrentId id, CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _catalogStore.SetShouldRunAsync(id, true, ct).ConfigureAwait(false);

            if (!_engineReady)
            {
                _commandQueue.SetDesiredRunning(id, true);
                await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
                PublishTorrentUpdates();
                FireAndForget(EnsureStartedAsync(ct), "ensure-started");
                return;
            }

            TorrentManager? m;
            await _opGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _byId.TryGetValue(id, out m);
            }
            finally
            {
                _opGate.Release();
            }

            if (m is null)
            {
                _commandQueue.SetDesiredRunning(id, true);
                await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
                PublishTorrentUpdates();
                return;
            }

            try
            {
                await m.StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start torrent {TorrentId}", id.Value);
                await NotifyAsync("Не удалось запустить торрент", $"Команда запуска завершилась ошибкой.\n\n{ex.Message}").ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task PauseAsync(TorrentId id, CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Pausing torrent {TorrentId}", id.Value);
            await _catalogStore.SetShouldRunAsync(id, false, ct).ConfigureAwait(false);

            if (!_engineReady)
            {
                _commandQueue.SetDesiredRunning(id, false);
                await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
                PublishTorrentUpdates();
                FireAndForget(EnsureStartedAsync(ct), "ensure-started");
                return;
            }

            TorrentManager? m;
            await _opGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _byId.TryGetValue(id, out m);
            }
            finally
            {
                _opGate.Release();
            }

            if (m is null)
            {
                _commandQueue.SetDesiredRunning(id, false);
                await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
                PublishTorrentUpdates();
                return;
            }

            await m.PauseAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(TorrentId id, CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Stopping torrent {TorrentId}", id.Value);
            await _catalogStore.SetShouldRunAsync(id, false, ct).ConfigureAwait(false);

            if (!_engineReady)
            {
                _commandQueue.SetDesiredRunning(id, false);
                await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
                PublishTorrentUpdates();
                FireAndForget(EnsureStartedAsync(ct), "ensure-started");
                return;
            }

            TorrentManager? m;
            await _opGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _byId.TryGetValue(id, out m);
            }
            finally
            {
                _opGate.Release();
            }

            if (m is null)
            {
                _commandQueue.SetDesiredRunning(id, false);
                await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
                PublishTorrentUpdates();
                return;
            }

            await m.StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task RemoveAsync(TorrentId id, bool deleteDownloadedData, CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Removing torrent {TorrentId}. Delete data: {DeleteDownloadedData}", id.Value, deleteDownloadedData);

            if (!_engineReady)
            {
                var cached = await _catalogStore.TryGetCachedAsync(id, ct).ConfigureAwait(false);
                if (cached is null)
                    return;

                await _catalogStore.RemoveAndRememberDeletionAsync(id, cached.Key, deleteDownloadedData, ct).ConfigureAwait(false);
                await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
                PublishTorrentUpdates();
                FireAndForget(EnsureStartedAsync(ct), "ensure-started-after-remove");
                return;
            }

            await EnsureStartedAsync(ct).ConfigureAwait(false);

            TorrentManager? m;
            await _opGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _byId.TryGetValue(id, out m);
                if (m is null)
                    return;
            }
            finally
            {
                _opGate.Release();
            }

            try
            {
                await m.StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

                var mode = deleteDownloadedData ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly;
                await Engine.RemoveAsync(m, mode).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove torrent {TorrentId}", id.Value);
                await NotifyAsync("Не удалось удалить торрент", $"Команда удаления завершилась ошибкой.\n\n{ex.Message}").ConfigureAwait(false);
                throw;
            }

            await _opGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _byId.Remove(id);
            }
            finally
            {
                _opGate.Release();
            }

            await _catalogStore.RemoveAsync(id, ct).ConfigureAwait(false);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);

            FireAndForget(SaveAsync(CancellationToken.None), "save-engine-state");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task PublishTorrentUpdatesAsync(CancellationToken ct = default)
    {
        var handler = UpdateTorrent;
        if (handler is null) return;

        if (_engine is null)
        {
            // Publish cached snapshots until the engine becomes ready.
            var cached = await _catalogStore.BuildCachedSnapshotsAsync(ct).ConfigureAwait(false);
            handler.Invoke(cached);
            return;
        }

        List<(TorrentId id, TorrentManager m)> managers;

        await _opGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            managers = _byId.Select(kv => (kv.Key, kv.Value)).ToList();
        }
        finally { _opGate.Release(); }

        // Build snapshots outside the gate to reduce lock contention.
        var snapshots = new List<TorrentSnapshot>(managers.Count);
        foreach (var (id, m) in managers)
        {
            // get stable AddedAtUtc from catalog if present
            var addedAt = await _catalogStore.TryGetAddedAtUtcAsync(id, ct).ConfigureAwait(false);
            snapshots.Add(BuildSnapshot(id, m, addedAtUtc: addedAt ?? DateTimeOffset.UtcNow));
        }

        // Persist runtime state used for fast UI restore on the next launch.
        foreach (var s in snapshots)
            await _catalogStore.UpsertFromSnapshotAsync(s, ct).ConfigureAwait(false);

        FireAndForget(_catalogStore.SaveAsync(force: false, CancellationToken.None), "save-catalog");

        handler.Invoke(snapshots);
    }

    // Synchronous wrapper for callers that cannot await updates directly.
    public void PublishTorrentUpdates()
        => FireAndForget(PublishTorrentUpdatesAsync(CancellationToken.None), "update-torrent");

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            await _saveGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await Engine.SaveStateAsync(_stateFilePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save torrent engine state");
                if (!ct.IsCancellationRequested)
                    throw;
            }
            finally
            {
                _saveGate.Release();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Shutting down torrent service");

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_engine is null && _initTask is null)
                return;

            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);

            if (_engine is not null)
            {
                await _saveGate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await Engine.SaveStateAsync(_stateFilePath).ConfigureAwait(false);
                }
                finally
                {
                    _saveGate.Release();
                }
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task<Torrent> ResolveTorrentAsync(TorrentSource source, CancellationToken ct)
    {
        switch (source)
        {
            case TorrentSource.TorrentFile tf:
                return await Torrent.LoadAsync(tf.Path).ConfigureAwait(false);

            case TorrentSource.Magnet m:
                await EnsureStartedAsync(ct).ConfigureAwait(false);
                var magnet = MagnetLink.Parse(m.Uri);
                var metadata = await Engine.DownloadMetadataAsync(magnet, ct).ConfigureAwait(false);
                return await Torrent.LoadAsync(memory: metadata.ToArray()).ConfigureAwait(false);

            case TorrentSource.TorrentBytes b:
                return await Torrent.LoadAsync(b.Bytes).ConfigureAwait(false);

            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    public async Task ApplySettingsAsync()
    {
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await EnsureStartedAsync(CancellationToken.None).ConfigureAwait(false);

            var b = new EngineSettingsBuilder(Engine.Settings)
            {
                CacheDirectory = _cacheDir,

                MaximumDownloadRate = _prefs.MaximumDownloadRate,
                MaximumUploadRate = _prefs.MaximumUploadRate,

                MaximumDiskReadRate = _prefs.MaximumDiskReadRate,
                MaximumDiskWriteRate = _prefs.MaximumDiskWriteRate,

                AllowPortForwarding = _prefs.AllowPortForwarding,
                AllowLocalPeerDiscovery = _prefs.AllowLocalPeerDiscovery,

                MaximumConnections = _prefs.MaximumConnections,
                MaximumOpenFiles = _prefs.MaximumOpenFiles,

                AutoSaveLoadFastResume = _prefs.AutoSaveLoadFastResume,
                AutoSaveLoadMagnetLinkMetadata = _prefs.AutoSaveLoadMagnetLinkMetadata,

                FastResumeMode = _prefs.FastResumeMode == TorrentFastResumeMode.Accurate
                    ? MonoTorrent.Client.FastResumeMode.Accurate
                    : MonoTorrent.Client.FastResumeMode.BestEffort
            };

            await Engine.UpdateSettingsAsync(b.ToSettings()).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private TorrentSnapshot BuildSnapshot(TorrentId id, TorrentManager m, DateTimeOffset? addedAtUtc)
    {
        var phase = m.State switch
        {
            TorrentState.Metadata => TorrentPhase.FetchingMetadata,
            TorrentState.Hashing or TorrentState.FetchingHashes => TorrentPhase.Checking,

            TorrentState.Downloading => TorrentPhase.Downloading,
            TorrentState.Seeding => TorrentPhase.Seeding,

            TorrentState.Paused => TorrentPhase.Paused,
            TorrentState.Stopped => TorrentPhase.Stopped,

            TorrentState.Error => TorrentPhase.Error,
            _ => TorrentPhase.Unknown
        };

        var progress = m.PartialProgress;
        var isComplete = progress >= 100.0;

        var status = new TorrentStatus(
            phase,
            isComplete,
            progress,
            m.Monitor.DownloadRate,
            m.Monitor.UploadRate,
            m.Error?.ToString(),
            TorrentSnapshotSource.Live
        );

        // Torrent may be null during metadata phase; keep safe defaults
        var size = m.Torrent?.Size ?? 0;

        return new TorrentSnapshot(
            id,
            Key: GetStableKey(m),
            Name: m.Name,
            Size: size,
            SavePath: m.SavePath,
            AddedAtUtc: addedAtUtc ?? DateTimeOffset.UtcNow,
            Status: status
        );
    }

    private async Task PublishCachedSnapshotsAsync(CancellationToken ct)
    {
        var handler = UpdateTorrent;
        if (handler is null) return;

        try
        {
            var snapshots = await _catalogStore.BuildCachedSnapshotsAsync(ct).ConfigureAwait(false);
            handler.Invoke(snapshots);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish cached snapshots");
        }
    }

    private async Task ApplyQueuedIntentAsync(CancellationToken ct)
    {
        var desired = _commandQueue.SnapshotAndClear();
        if (desired.Count == 0)
            return;

        foreach (var (id, shouldRun) in desired)
        {
            TorrentManager? m;

            await _opGate.WaitAsync(ct).ConfigureAwait(false);
            try { _byId.TryGetValue(id, out m); }
            finally { _opGate.Release(); }

            if (m is null) continue;

            try
            {
                if (shouldRun)
                    await m.StartAsync().ConfigureAwait(false);
                else
                    await m.StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply queued command for torrent {TorrentId}", id.Value);
            }
        }

        await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
    }

    private async Task AutoStartFromCatalogAsync(CancellationToken ct)
    {
        List<(TorrentId id, TorrentManager m)> managers;

        await _opGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            managers = _byId.Select(kv => (kv.Key, kv.Value)).ToList();
        }
        finally { _opGate.Release(); }

        foreach (var (id, m) in managers)
        {
            var (found, shouldRun) = await _catalogStore.TryGetShouldRunAsync(id, ct).ConfigureAwait(false);
            if (!found || !shouldRun)
                continue;

            if (m.State is TorrentState.Downloading or TorrentState.Seeding or TorrentState.Hashing or TorrentState.FetchingHashes or TorrentState.Metadata)
                continue;

            try
            {
                await m.StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-start torrent {TorrentId}", id.Value);
            }
        }

        await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
    }


    private async Task<ClientEngine> CreateEngineWithRecoveryAsync(CancellationToken ct)
    {
        if (!File.Exists(_stateFilePath))
            return CreateFreshEngine();

        try
        {
            return await ClientEngine.RestoreStateAsync(_stateFilePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var brokenPath = MoveBrokenStateFile();
            _logger.LogWarning(ex, "Failed to restore torrent engine state from {StateFilePath}. Backup created at {BrokenPath}", _stateFilePath, brokenPath);
            await NotifyAsync(
                "Повреждён torrent_engine.dat",
                $"Сохранённое состояние движка повреждено или несовместимо. Будет выполнен запуск с чистым состоянием.\n\nРезервная копия: {brokenPath}").ConfigureAwait(false);
            return CreateFreshEngine();
        }
    }

    public ClientEngine CreateFreshEngine()
    {
        var settings = new EngineSettingsBuilder
        {
            CacheDirectory = _cacheDir,
        }.ToSettings();

        return new ClientEngine(settings);
    }

    private string MoveBrokenStateFile()
    {
        try
        {
            var brokenPath = Path.Combine(
                Path.GetDirectoryName(_stateFilePath)!,
                $"torrent_engine.broken-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.dat");

            if (File.Exists(brokenPath))
                File.Delete(brokenPath);

            File.Move(_stateFilePath, brokenPath, overwrite: true);
            return brokenPath;
        }
        catch (Exception moveEx)
        {
            _logger.LogWarning(moveEx, "Failed to move broken torrent engine state file {StateFilePath}", _stateFilePath);
            return _stateFilePath;
        }
    }

    public Task NotifyAsync(string title, string message)
        => _dialogs.ShowTextAsync(title, message);

    private static string GetStableKey(TorrentManager m)
    {
        try
        {
            var infoHashes = m.InfoHashes;
            var v1 = infoHashes?.V1;
            if (v1 is not null)
                return v1.ToString() ?? string.Empty;

            var v2 = infoHashes?.V2;
            return v2?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void FireAndForget(Task task, string name)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception is not null)
                _logger.LogError(t.Exception, "Background task failed: {Name}", name);
        }, TaskScheduler.Default);
    }
}
