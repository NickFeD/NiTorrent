using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Common;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentAddExecutor
{
    private readonly ILogger<TorrentAddExecutor> _logger;
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly ITorrentCollectionRepository _collectionRepository;
    private readonly TorrentStableKeyAccessor _stableKeyAccessor;

    public TorrentAddExecutor(
        ILogger<TorrentAddExecutor> logger,
        TorrentRuntimeRegistry runtimeRegistry,
        ITorrentCollectionRepository collectionRepository,
        TorrentStableKeyAccessor stableKeyAccessor)
    {
        _logger = logger;
        _runtimeRegistry = runtimeRegistry;
        _collectionRepository = collectionRepository;
        _stableKeyAccessor = stableKeyAccessor;
    }

    public async Task<TorrentId> AddAsync(
        ClientEngine engine,
        AddTorrentRequest request,
        Func<TorrentSource, CancellationToken, Task<Torrent>> resolveTorrentAsync,
        SemaphoreSlim opGate,
        CancellationToken ct)
    {
        _logger.LogInformation("Adding torrent");

        var torrent = await resolveTorrentAsync(request.Source, ct).ConfigureAwait(false);
        var stableKey = new TorrentKey(_stableKeyAccessor.GetStableKey(torrent));

        var existing = TorrentDuplicatePolicy.FindDuplicate(
            await _collectionRepository.GetAllAsync(ct).ConfigureAwait(false),
            stableKey,
            torrent.Name,
            request.SavePath);

        if (existing is not null)
            throw new UserVisibleException("Этот торрент уже добавлен в приложение.");

        var manager = await engine.AddAsync(torrent, request.SavePath).ConfigureAwait(false);
        await ApplyFileSelectionAsync(manager, request.SelectedFilePaths).ConfigureAwait(false);

        var id = new TorrentId(Guid.NewGuid());
        var addedAt = DateTimeOffset.UtcNow;

        await opGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _runtimeRegistry.Set(id, manager);
        }
        finally
        {
            opGate.Release();
        }

        var selectedFiles = request.SelectedFilePaths is { Count: > 0 }
            ? request.SelectedFilePaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();

        var phase = manager.State switch
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

        var progress = manager.PartialProgress;
        var runtime = new TorrentRuntimeState(
            TorrentLifecycleStateMapper.FromPhase(phase),
            progress >= 100.0,
            progress,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            manager.Error?.ToString(),
            isEngineBacked: true);

        var status = new TorrentStatus(
            phase,
            runtime.IsComplete,
            runtime.Progress,
            runtime.DownloadRateBytesPerSecond,
            runtime.UploadRateBytesPerSecond,
            runtime.Error,
            TorrentStatusSource.Live);

        var entry = new TorrentEntry(
            id,
            stableKey,
            manager.Name,
            manager.Torrent?.Size ?? torrent.Size,
            request.SavePath,
            addedAt,
            TorrentIntent.Running,
            runtime.LifecycleState,
            runtime,
            status,
            HasMetadata: true,
            SelectedFiles: selectedFiles,
            PerTorrentSettings: null,
            DeferredActions: Array.Empty<DeferredAction>());

        await _collectionRepository.UpsertAsync(entry, ct).ConfigureAwait(false);
        await _collectionRepository.SaveAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Starting torrent {TorrentId}", id.Value);
        await manager.StartAsync().ConfigureAwait(false);

        return id;
    }

    private static async Task ApplyFileSelectionAsync(TorrentManager manager, IReadOnlySet<string>? selectedFilePaths)
    {
        if (selectedFilePaths is not { Count: > 0 })
            return;

        var selected = new HashSet<string>(selectedFilePaths, StringComparer.OrdinalIgnoreCase);
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
}
