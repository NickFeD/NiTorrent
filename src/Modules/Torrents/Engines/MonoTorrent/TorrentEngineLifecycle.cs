using Microsoft.Extensions.Logging;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents.Abstract;
using System.Diagnostics;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>Owns the concrete engine resources; session owns torrent preparation.</summary>
internal sealed class TorrentEngineLifecycle(
    IEngineSettingsService settings,
    TorrentEngineCoordinator coordinator,
    TorrentRuntimeWorkGate workGate,
    ILogger<TorrentEngineLifecycle> logger) : ITorrentEngineLifecycle
{
    private int _started;
    public async Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        var elapsed = Stopwatch.StartNew();
        try
        {
            await settings.InitializeAsync(ct).ConfigureAwait(false);
            logger.LogInformation("[Lifecycle] Engine ready in {ElapsedMs} ms", elapsed.ElapsedMilliseconds);
        }
        catch
        {
            coordinator.DisposeRuntime();
            Interlocked.Exchange(ref _started, 0);
            throw;
        }
    }
    public Task StopAcceptingWorkAsync(CancellationToken ct)
    {
        workGate.StopAcceptingWork();
        return Task.CompletedTask;
    }
    public async Task StopAsync(CancellationToken ct)
    {
        var errors = new List<Exception>();
        foreach (var manager in coordinator.GetTorrentMap().Values)
        {
            ct.ThrowIfCancellationRequested();
            try { await manager.StopAsync(TimeSpan.FromSeconds(3)).WaitAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                errors.Add(ex);
                logger.LogWarning(ex, "Failed to stop {TorrentName}", manager.Name);
            }
        }
        if (errors.Count > 0) throw new AggregateException("Some managers failed to stop.", errors);
    }
    public void DisposeRuntime() => coordinator.DisposeRuntime();
}
