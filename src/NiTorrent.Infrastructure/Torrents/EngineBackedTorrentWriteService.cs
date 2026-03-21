using MonoTorrent.Client;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Transitional write boundary that executes torrent write scenarios directly against
/// infrastructure-owned engine/runtime components instead of delegating to the legacy
/// <see cref="legacy compatibility service"/> facade.
/// </summary>
public sealed class EngineBackedTorrentWriteService(
    TorrentStartupCoordinator startupCoordinator,
    TorrentRuntimeContext runtimeContext,
    TorrentLifecycleExecutor lifecycleExecutor,
    TorrentCommandExecutor commandExecutor,
    TorrentAddExecutor addExecutor,
    TorrentSourceResolver sourceResolver,
    TorrentSettingsApplier settingsApplier,
    TorrentEventOrchestrator eventOrchestrator,
    BackgroundTaskRunner backgroundTasks,
    TorrentNotifier notifier,
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
                onBackgroundTaskScheduled: null,
                ct).ConfigureAwait(false);

            eventOrchestrator.PublishUpdatesInBackground(startupCoordinator.Engine is not null, runtimeContext.OperationGate);
            backgroundTasks.Run(SaveEngineStateAsync(CancellationToken.None), "save-engine-state");
            return id;
        }, ct);

    public Task StartAsync(TorrentId id, CancellationToken ct = default)
        => RunCommandWithNotificationAsync(
            async token =>
            {
                await commandExecutor.StartAsync(
                    id,
                    startupCoordinator.IsReady,
                    runtimeContext.OperationGate,
                    runtimeContext.CommandQueue,
                    EnsureStartedAsync,
                    PublishTorrentUpdates,
                    backgroundTasks.Run,
                    token).ConfigureAwait(false);
            },
            "Не удалось запустить торрент",
            "Команда запуска завершилась ошибкой.",
            ct);

    public Task PauseAsync(TorrentId id, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await commandExecutor.PauseAsync(
                id,
                startupCoordinator.IsReady,
                runtimeContext.OperationGate,
                runtimeContext.CommandQueue,
                EnsureStartedAsync,
                PublishTorrentUpdates,
                backgroundTasks.Run,
                ct).ConfigureAwait(false);
        }, ct);


    public Task StopAsync(TorrentId id, CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await commandExecutor.StopAsync(
                id,
                startupCoordinator.IsReady,
                runtimeContext.OperationGate,
                runtimeContext.CommandQueue,
                EnsureStartedAsync,
                PublishTorrentUpdates,
                backgroundTasks.Run,
                ct).ConfigureAwait(false);
        }, ct);

    public Task RemoveAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
        => RunCommandWithNotificationAsync(
            async token =>
            {
                await commandExecutor.RemoveAsync(
                    id,
                    deleteData,
                    startupCoordinator.IsReady,
                    startupCoordinator.Engine,
                    runtimeContext.OperationGate,
                    runtimeContext.CommandQueue,
                    EnsureStartedAsync,
                    PublishTorrentUpdates,
                    backgroundTasks.Run,
                    token).ConfigureAwait(false);
            },
            "Не удалось удалить торрент",
            "Команда удаления завершилась ошибкой.",
            ct);

    public Task ApplySettingsAsync(CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);
            var cacheDir = storage.GetCachePath(@"Torrents\cache");
            storage.EnsureDirectory(cacheDir);
            await settingsApplier.ApplyAsync(Engine, cacheDir).ConfigureAwait(false);
        }, ct);

    private Task EnsureStartedAsync(CancellationToken ct = default)
        => startupCoordinator.EnsureStartedAsync(runtimeContext.OperationGate, runtimeContext.CommandQueue, eventOrchestrator.RaiseLoaded, ct);

    private void PublishTorrentUpdates()
        => eventOrchestrator.PublishUpdatesInBackground(startupCoordinator.Engine is not null, runtimeContext.OperationGate);

    private async Task SaveEngineStateAsync(CancellationToken ct)
    {
        if (startupCoordinator.Engine is null)
            return;

        await lifecycleExecutor.RunAsync(async () =>
        {
            await engineStateStore.SaveAsync(Engine, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private Task RunCommandWithNotificationAsync(
        Func<CancellationToken, Task> action,
        string title,
        string messagePrefix,
        CancellationToken ct)
        => lifecycleExecutor.RunAsync(async () =>
        {
            try
            {
                await action(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await notifier.NotifyAsync(title, $"{messagePrefix}\n\n{ex.Message}").ConfigureAwait(false);
                throw;
            }
        }, ct);
}
