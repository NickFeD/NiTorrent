using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class InfrastructureTorrentEngineStateStore(
    TorrentStartupCoordinator startupCoordinator,
    TorrentEngineStateStore engineStateStore,
    TorrentLifecycleExecutor lifecycleExecutor) : ITorrentEngineStateStore
{
    public Task SaveAsync(CancellationToken ct = default)
        => lifecycleExecutor.RunAsync(async () =>
        {
            if (startupCoordinator.Engine is not null)
                await engineStateStore.SaveAsync(startupCoordinator.Engine, ct).ConfigureAwait(false);
        }, ct);
}
