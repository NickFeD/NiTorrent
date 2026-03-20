using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentAddExecutor
{
    private readonly ILogger<TorrentAddExecutor> _logger;
    private readonly TorrentCatalogStore _catalogStore;
    private readonly TorrentSnapshotFactory _snapshotFactory;
    private readonly TorrentRuntimeRegistry _runtimeRegistry;

    public TorrentAddExecutor(
        ILogger<TorrentAddExecutor> logger,
        TorrentCatalogStore catalogStore,
        TorrentSnapshotFactory snapshotFactory,
        TorrentRuntimeRegistry runtimeRegistry)
    {
        _logger = logger;
        _catalogStore = catalogStore;
        _snapshotFactory = snapshotFactory;
        _runtimeRegistry = runtimeRegistry;
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

        try
        {
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

            var snapshot = _snapshotFactory.Create(id, manager, addedAtUtc: addedAt) with
            {
                Key = _snapshotFactory.GetStableKey(torrent)
            };
            await _catalogStore.UpsertFromSnapshotAsync(snapshot, ct).ConfigureAwait(false);
            await _catalogStore.SetShouldRunAsync(id, true, ct).ConfigureAwait(false);
            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);

            _logger.LogInformation("Starting torrent {TorrentId}", id.Value);
            await manager.StartAsync().ConfigureAwait(false);

            return id;
        }
        catch (Exception ex) when (IsDuplicateManagerError(ex))
        {
            throw new InvalidOperationException("Этот торрент уже добавлен в приложение.", ex);
        }
    }

    private static bool IsDuplicateManagerError(Exception ex)
        => ex.Message.Contains("already been registered", StringComparison.OrdinalIgnoreCase);

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
