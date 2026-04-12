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
    private readonly PeerEndpointConnectionCooldown _peerEndpointCooldown;

    public TorrentAddExecutor(
        ILogger<TorrentAddExecutor> logger,
        TorrentRuntimeRegistry runtimeRegistry,
        PeerEndpointConnectionCooldown peerEndpointCooldown)
    {
        _logger = logger;
        _runtimeRegistry = runtimeRegistry;
        _peerEndpointCooldown = peerEndpointCooldown;
    }

    public async Task<TorrentRuntimeStateOld> AddAsync(
        ClientEngine engine,
        TorrentId id,
        AddTorrentRequest request,
        SemaphoreSlim opGate,
        bool startImmediately,
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

        _peerEndpointCooldown.Register(id, manager);

        if (startImmediately)
        {
            _logger.LogInformation("Starting torrent {TorrentId}", id.Value);
            await manager.StartAsync().ConfigureAwait(false);
        }

        var phase = manager.State switch
        {
            TorrentState.Metadata => TorrentLifecycleStateOld.FetchingMetadata,
            TorrentState.Hashing or TorrentState.FetchingHashes => TorrentLifecycleStateOld.Checking,
            TorrentState.Downloading => TorrentLifecycleStateOld.Downloading,
            TorrentState.Seeding => TorrentLifecycleStateOld.Seeding,
            TorrentState.Paused => TorrentLifecycleStateOld.Paused,
            TorrentState.Stopped => TorrentLifecycleStateOld.Stopped,
            TorrentState.Error => TorrentLifecycleStateOld.Error,
            _ => TorrentLifecycleStateOld.Unknown
        };

        var progress = manager.PartialProgress;
        return new TorrentRuntimeStateOld(
            new object(),
            progress >= 100.0,
            progress,
            int.MaxValue,
            int.MaxValue,
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
