using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class InfrastructureTorrentEngineLifecycle(
    TorrentStartupCoordinator startupCoordinator,
    TorrentRuntimeContext runtimeContext,
    TorrentEventOrchestrator eventOrchestrator) : ITorrentEngineLifecycle
{
    public Task InitializeAsync(CancellationToken ct = default)
        => startupCoordinator.EnsureStartedAsync(runtimeContext.OperationGate, eventOrchestrator.RaiseLoaded, ct);

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await startupCoordinator.ShutdownAsync(runtimeContext.OperationGate, ct).ConfigureAwait(false);
        eventOrchestrator.InvalidateRuntime();
    }
}
