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
    TorrentEventOrchestrator eventOrchestrator,
    BackgroundTaskRunner backgroundTasks,
    TorrentEngineStateStore engineStateStore,
    IAppStorageService storage) : ITorrentWriteService
{
    private ClientEngine Engine
        => startupCoordinator.Engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");

    public Task<TorrentRuntimeState> AddAsync(TorrentId id, AddTorrentRequest request, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            var runtime = await addExecutor.AddAsync(
                Engine,
                id,
                request,
                runtimeContext.OperationGate,
                startImmediately: true,
                ct).ConfigureAwait(false);

            eventOrchestrator.InvalidateRuntime();
            backgroundTasks.Run(SaveEngineStateAsync(CancellationToken.None), "save-engine-state");
            return runtime;
        }, ct);

    public Task<TorrentRuntimeState> RehydrateAsync(TorrentEntry entry, byte[] torrentBytes, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            var existing = await TryGetExistingManagerAsync(entry.Id, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                await ApplyRuntimeSettingsAsync(entry, ct).ConfigureAwait(false);
                return MapRuntime(existing);
            }

            var request = new AddTorrentRequest(
                new PreparedTorrentSource(
                    torrentBytes,
                    entry.Key,
                    entry.Name,
                    entry.Size,
                    Array.Empty<TorrentFileEntry>(),
                    entry.HasMetadata),
                entry.SavePath,
                entry.SelectedFiles.Count == 0
                    ? null
                    : entry.SelectedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase));

            var runtime = await addExecutor.AddAsync(
                Engine,
                entry.Id,
                request,
                runtimeContext.OperationGate,
                startImmediately: entry.Intent == TorrentIntent.Running,
                ct).ConfigureAwait(false);

            await ApplyRuntimeSettingsAsync(entry, ct).ConfigureAwait(false);

            eventOrchestrator.InvalidateRuntime();
            return runtime;
        }, ct);

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

    private static TorrentRuntimeState MapRuntime(TorrentManager manager)
    {
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
            IsEngineBacked: true);
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
