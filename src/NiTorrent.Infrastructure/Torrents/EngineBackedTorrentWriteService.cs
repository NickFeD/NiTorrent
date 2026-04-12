using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Infrastructure-backed add/settings execution.
/// Product decisions (duplicate policy, entry identity, persistence) live in Application.
/// </summary>
public sealed class EngineBackedTorrentWriteService(
    TorrentStartupCoordinator startupCoordinator,
    TorrentRuntimeContext runtimeContext,
    TorrentLifecycleExecutor lifecycleExecutor,
    TorrentAddExecutor addExecutor,
    TorrentSettingsApplier settingsApplier,
    ITorrentEntrySettingsRuntimeApplier runtimeSettingsApplier,
    TorrentRuntimeRegistry runtimeRegistry,
    PeerEndpointConnectionCooldown peerEndpointCooldown,
    TorrentEventOrchestrator eventOrchestrator,
    BackgroundTaskRunner backgroundTasks,
    TorrentEngineStateStore engineStateStore,
    IAppStorageService storage) : ITorrentWriteService
{
    private ClientEngine Engine
        => startupCoordinator.Engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");

    public async Task<TorrentRuntimeStateOld> AddAsync(TorrentId id, AddTorrentRequest request, CancellationToken ct = default)
        => await addExecutor.AddAsync(
                Engine,
                id,
                request,
                runtimeContext.OperationGate,
                startImmediately: true,
                ct).ConfigureAwait(false);

    public Task<TorrentRuntimeStateOld> RehydrateAsync(TorrentEntry entry, byte[] torrentBytes, CancellationToken ct = default)
        => MapRuntime(Engine.Torrents[0]);

    public Task ApplySettingsAsync(CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);
            var cacheDir = storage.GetCachePath(@"Torrents\cache");
            storage.EnsureDirectory(cacheDir);
            await settingsApplier.ApplyAsync(Engine, cacheDir).ConfigureAwait(false);
            eventOrchestrator.InvalidateRuntime();
        }, ct);

    private Task EnsureStartedAsync(CancellationToken ct = default)
        => startupCoordinator.EnsureStartedAsync(runtimeContext.OperationGate, eventOrchestrator.RaiseLoaded, ct);

    private async Task<TorrentManager?> TryGetExistingManagerAsync(TorrentId id, CancellationToken ct)
    {
        await runtimeContext.OperationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            runtimeRegistry.TryGet(id, out var manager);
            return manager;
        }
        finally
        {
            runtimeContext.OperationGate.Release();
        }
    }

    private async Task ApplyRuntimeSettingsAsync(TorrentEntry entry, CancellationToken ct)
    {
        if (entry.PerTorrentSettings is null)
            return;

        await runtimeSettingsApplier.ApplyAsync(entry.Id, entry.PerTorrentSettings, ct).ConfigureAwait(false);
    }

    private static Task<TorrentRuntimeStateOld> MapRuntime(TorrentManager manager)
    {
        var phase = manager.State switch
        {
            TorrentState.Metadata => TorrentLifecycleStateOld.FetchingMetadata,
            TorrentState.Hashing or TorrentState.FetchingHashes => TorrentLifecycleStateOld .Checking,
            TorrentState.Downloading => TorrentLifecycleStateOld.Downloading,
            TorrentState.Seeding => TorrentLifecycleStateOld.Seeding,
            TorrentState.Paused => TorrentLifecycleStateOld.Paused,
            TorrentState.Stopped => TorrentLifecycleStateOld.Stopped,
            TorrentState.Error => TorrentLifecycleStateOld.Error,
            _ => TorrentLifecycleStateOld.Unknown
        };

        var progress = manager.PartialProgress;
        return Task.FromResult(new TorrentRuntimeStateOld(
            new object(),
            progress >= 100.0,
            progress,
            int.MaxValue,
            int.MaxValue,
            manager.Error?.ToString(),
            true));
    }

    private async Task SaveEngineStateAsync(CancellationToken ct)
    {
        if (startupCoordinator.Engine is null)
            return;

        await lifecycleExecutor.RunAsync(async () =>
        {
            await engineStateStore.SaveAsync(Engine, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }
}
