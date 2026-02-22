using System.Reflection;
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

    private readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly string _cacheDir;
    private readonly string _stateFilePath;

    private readonly object _initGate = new();
    private Task? _initTask;

    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private ClientEngine? _engine;

    // внутреннее соответствие TorrentId -> TorrentManager
    private readonly Dictionary<TorrentId, TorrentManager> _byId = new();

    public event Action? Loaded;

    public event Action<IReadOnlyList<TorrentSnapshot>>? UptateTorrent;
    public MonoTorrentService(
    ILogger<MonoTorrentService> logger,
    IAppStorageService storage,
    ITorrentPreferences prefs)
    {
        _logger = logger;
        _storage = storage;
        _prefs = prefs;

        _cacheDir = _storage.GetCachePath(@"Torrents\cache");
        _stateFilePath = _storage.GetLocalPath(@"Torrents\torrent_engine.dat");

        _storage.EnsureDirectory(_cacheDir);
        _storage.EnsureParentDirectory(_stateFilePath);
    }

    private ClientEngine Engine
        => _engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");

    public Task InitializeAsync(CancellationToken ct = default) => EnsureStartedAsync(ct);

    private async Task LoadEngineInternalAsync(CancellationToken ct)
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                _engine = await ClientEngine.RestoreStateAsync(_stateFilePath);
            }
            else
            {
                var settings = new EngineSettingsBuilder
                {
                    CacheDirectory = _cacheDir,
                }.ToSettings();

                _engine = new ClientEngine(settings);
            }

            // назначаем id уже восстановленным менеджерам
            foreach (var m in Engine.Torrents)
                _byId[new TorrentId(Guid.NewGuid())] = m;

            Loaded?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize torrent engine");
            throw;
        }
    }
    private Task EnsureStartedAsync(CancellationToken ct = default)
    {
        if (_initTask is not null) return _initTask;

        return StartOnceAsync(ct);

        async Task StartOnceAsync(CancellationToken ct = default)
        {
            await _initLock.WaitAsync();
            try
            {
                _initTask ??= LoadEngineInternalAsync(ct);
            }
            finally { _initLock.Release(); }

            await _initTask;
        }
    }

    public IReadOnlyList<TorrentSnapshot> GetAll()
        => _byId.Select(kv => BuildSnapshot(kv.Key, kv.Value)).ToList();

    public TorrentSnapshot? TryGet(TorrentId id)
        => _byId.TryGetValue(id, out var m) ? BuildSnapshot(id, m) : null;

    public async Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
    {
        await EnsureStartedAsync(ct);
        var torrent = await ResolveTorrentAsync(source, ct);

        var files = torrent.Files
            .Select(f => new TorrentFileEntry(f.Path, f.Length, true))
            .ToList();

        return new TorrentPreview(torrent.Name, torrent.Size, files);
    }

    public async Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default)
    {
        var torrent = await ResolveTorrentAsync(request.Source, ct).ConfigureAwait(false);

        var manager = await Engine.AddAsync(torrent, request.SavePath).ConfigureAwait(false);

        if (request.SelectedFilePaths is { Count: > 0 })
        {
            var selected = new HashSet<string>(request.SelectedFilePaths, StringComparer.OrdinalIgnoreCase);

            // батчами, чтобы не породить шторм тасков
            var batch = new List<Task>(256);
            foreach (var file in manager.Files)
            {
                if (selected.Contains(file.Path)) continue;

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

        // сначала зарегистрируй в словарях, чтобы UI мог показать сразу
        var id = new TorrentId(Guid.NewGuid());
        _byId[id] = manager;

        // старт — лучше не на UI контексте
        await manager.StartAsync().ConfigureAwait(false);

        // сохранение — не обязано быть блокирующим перед возвратом id
        _ = Task.Run(() => SaveAsync(ct), CancellationToken.None);

        return id;
    }


    public async Task StartAsync(TorrentId id, CancellationToken ct = default)
    {
        if (_byId.TryGetValue(id, out var m))
        {
            await m.StartAsync();
        }
    }

    public async Task PauseAsync(TorrentId id, CancellationToken ct = default)
    {
        if (_byId.TryGetValue(id, out var m))
        {
            await m.PauseAsync();
        }
    }

    public async Task StopAsync(TorrentId id, CancellationToken ct = default)
    {
        if (_byId.TryGetValue(id, out var m))
        {
            await m.StopAsync(TimeSpan.FromSeconds(3));
        }
    }

    public async Task RemoveAsync(TorrentId id, bool deleteDownloadedData, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(id, out var m))
            return;

        await m.StopAsync(TimeSpan.FromSeconds(3));

        var mode = deleteDownloadedData ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly;
        await Engine.RemoveAsync(m, mode);

        _byId.Remove(id);
        await SaveAsync(ct);
    }

    public void UpdateTorrent()
    {
        var items = GetAll();

        if (UptateTorrent is not null)
            UptateTorrent.Invoke(items);
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _saveGate.WaitAsync(ct);
        try
        {
            await Engine.SaveStateAsync(_stateFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save torrent engine state");
            throw;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async Task<Torrent> ResolveTorrentAsync(TorrentSource source, CancellationToken ct)
    {
        switch (source)
        {
            case TorrentSource.TorrentFile tf:
                return await Torrent.LoadAsync(tf.Path);

            case TorrentSource.Magnet m:
                var magnet = MagnetLink.Parse(m.Uri);
                var metadata = await Engine.DownloadMetadataAsync(magnet, ct); // ReadOnlyMemory<byte>
                return await Torrent.LoadAsync(memory: metadata.ToArray());

            case TorrentSource.TorrentBytes b:
                return await Torrent.LoadAsync(b.Bytes);

            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    public async Task ApplySettingsAsync()
    {
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

        await Engine.UpdateSettingsAsync(b.ToSettings());
    }


    private static TorrentSnapshot BuildSnapshot(TorrentId id, TorrentManager m)
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

        var isComplete = m.PartialProgress >= 100.0;
        var progress = m.PartialProgress;

        var status = new TorrentStatus(
            phase,
            isComplete,
            progress,
            m.Monitor.DownloadRate,
            m.Monitor.UploadRate,
            m.Error?.ToString()
        );

        // key/savePath/name пока без “индекса и infohash”
        return new TorrentSnapshot(
            id,
            Key: "",
            Name: m.Name,
            Size: m.Torrent!.Size,
            SavePath: m.SavePath,
            AddedAtUtc: DateTimeOffset.UtcNow,
            Status: status
        );
    }

}
