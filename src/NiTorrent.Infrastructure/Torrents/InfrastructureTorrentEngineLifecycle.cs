using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class InfrastructureTorrentEngineLifecycle(
    TorrentStartupCoordinator startupCoordinator,
    TorrentRuntimeContext runtimeContext,
    TorrentEventOrchestrator eventOrchestrator) : ITorrentEngineLifecycle
{
    public Task InitializeAsync(CancellationToken ct = default)
        => startupCoordinator.EnsureStartedAsync(runtimeContext.OperationGate, eventOrchestrator.RaiseLoaded, ct);

    public Task ShutdownAsync(CancellationToken ct = default)
        => Task.CompletedTask;
}
