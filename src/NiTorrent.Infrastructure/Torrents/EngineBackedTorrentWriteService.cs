using MonoTorrent.Client;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Infrastructure-backed write boundary for preview/add/apply-settings scenarios.
/// Command scenarios are handled by ITorrentCommandService.
/// </summary>
public sealed class EngineBackedTorrentWriteService(
    TorrentStartupCoordinator startupCoordinator,
    TorrentRuntimeContext runtimeContext,
    TorrentLifecycleExecutor lifecycleExecutor,
    TorrentAddExecutor addExecutor,
    TorrentSourceResolver sourceResolver,
    TorrentSettingsApplier settingsApplier,
    TorrentEventOrchestrator eventOrchestrator,
    BackgroundTaskRunner backgroundTasks,
    TorrentEngineStateStore engineStateStore,
    NiTorrent.Application.Abstractions.IAppStorageService storage) : ITorrentWriteService
{
    private ClientEngine Engine
        => startupCoordinator.Engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");

    public async Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
    {
        var torrent = await sourceResolver
            .ResolveAsync(source, EnsureStartedAsync, () => Engine, ct)
            .ConfigureAwait(false);

        var files = torrent.Files
            .Select(f => new TorrentFileEntry(f.Path, f.Length, true))
            .ToList();

        return new TorrentPreview(torrent.Name, torrent.Size, files);
    }

    public Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            var id = await addExecutor.AddAsync(
                Engine,
                request,
                (source, token) => sourceResolver.ResolveAsync(source, EnsureStartedAsync, () => Engine, token),
                runtimeContext.OperationGate,
                ct).ConfigureAwait(false);

            eventOrchestrator.InvalidateRuntime();
            backgroundTasks.Run(SaveEngineStateAsync(CancellationToken.None), "save-engine-state");
            return id;
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
