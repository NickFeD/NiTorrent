using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class MonoTorrentService : ITorrentService
{
    private readonly ILogger<MonoTorrentService> _logger;
    private readonly IAppStorageService _storage;

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
    IAppStorageService storage)
    {
        _logger = logger;
        _storage = storage;

        _cacheDir = _storage.GetCachePath(@"Torrents\cache");
        _stateFilePath = _storage.GetLocalPath(@"Torrents\torrent_engine.dat");

        _storage.EnsureDirectory(_cacheDir);
        _storage.EnsureParentDirectory(_stateFilePath);
    }

    private ClientEngine Engine
        => _engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");

    public Task InitializeAsync(CancellationToken ct = default)
    {
        if (_engine == null)
            lock (_initGate)
            {
                if (_engine is null)
                {
                    _initTask ??= LoadEngineInternalAsync(ct);
                    return _initTask;
                }
                
            }
        return Task.CompletedTask;
    }

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
                    CacheDirectory = _cacheDir
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

    public IReadOnlyList<TorrentSnapshot> GetAll()
        => _byId.Select(kv => BuildSnapshot(kv.Key, kv.Value)).ToList();

    public TorrentSnapshot? TryGet(TorrentId id)
        => _byId.TryGetValue(id, out var m) ? BuildSnapshot(id, m) : null;

    public async Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
    {

        var torrent = await ResolveTorrentAsync(source, ct);

        var files = torrent.Files
            .Select(f => new TorrentFileEntry(f.Path, f.Length, true))
            .ToList();

        return new TorrentPreview(torrent.Name, torrent.Size, files);
    }

    public async Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default)
    {

        var torrent = await ResolveTorrentAsync(request.Source, ct);

        var manager = await Engine.AddAsync(torrent, request.SavePath);

        // выбор файлов — как у тебя в UTorrent: через SetFilePriorityAsync и file.Path
        if (request.SelectedFilePaths is { Count: > 0 })
        {
            var selected = new HashSet<string>(request.SelectedFilePaths, StringComparer.OrdinalIgnoreCase);

            foreach (var file in manager.Files)
            {
                if (!selected.Contains(file.Path))
                    await manager.SetFilePriorityAsync(file, Priority.DoNotDownload);
            }
        }

        await manager.StartAsync();
        await SaveAsync(ct);

        var id = new TorrentId(Guid.NewGuid());
        _byId[id] = manager;
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

    private async Task SaveAsync(CancellationToken ct)
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
                return await Torrent.LoadAsync(tf.path);

            case TorrentSource.Magnet m:
                var magnet = MagnetLink.Parse(m.Uri);
                var metadata = await Engine.DownloadMetadataAsync(magnet, ct); // ReadOnlyMemory<byte>
                return await Torrent.LoadAsync(memory: metadata.ToArray());

            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
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
