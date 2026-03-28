using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentAddExecutor
{
    private readonly ILogger<TorrentAddExecutor> _logger;
    private readonly TorrentRuntimeRegistry _runtimeRegistry;

    public TorrentAddExecutor(
        ILogger<TorrentAddExecutor> logger,
        TorrentRuntimeRegistry runtimeRegistry)
    {
        _logger = logger;
        _runtimeRegistry = runtimeRegistry;
    }

    public async Task<TorrentRuntimeState> AddAsync(
        ClientEngine engine,
        TorrentId id,
        AddTorrentRequest request,
        SemaphoreSlim opGate,
        CancellationToken ct)
    {
        _logger.LogInformation("Adding torrent {TorrentId}", id.Value);

        var torrent = await Torrent.LoadAsync(request.PreparedSource.TorrentBytes).ConfigureAwait(false);
        var manager = await engine.AddAsync(torrent, request.SavePath).ConfigureAwait(false);
        await ApplyFileSelectionAsync(manager, request.SelectedFilePaths).ConfigureAwait(false);

        await opGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _runtimeRegistry.Set(id, manager);
        }
        finally
        {
            opGate.Release();
        }

        _logger.LogInformation("Starting torrent {TorrentId}", id.Value);
        await manager.StartAsync().ConfigureAwait(false);

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
        return new TorrentRuntimeState(
            TorrentLifecycleStateMapper.FromPhase(phase),
            progress >= 100.0,
            progress,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            manager.Error?.ToString(),
            true);
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
