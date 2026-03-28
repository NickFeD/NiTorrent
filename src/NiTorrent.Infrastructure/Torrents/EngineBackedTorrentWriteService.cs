using MonoTorrent.Client;
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
    TorrentEventOrchestrator eventOrchestrator,
    BackgroundTaskRunner backgroundTasks,
    TorrentEngineStateStore engineStateStore,
    NiTorrent.Application.Abstractions.IAppStorageService storage) : ITorrentWriteService
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
                ct).ConfigureAwait(false);

            eventOrchestrator.InvalidateRuntime();
            backgroundTasks.Run(SaveEngineStateAsync(CancellationToken.None), "save-engine-state");
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
