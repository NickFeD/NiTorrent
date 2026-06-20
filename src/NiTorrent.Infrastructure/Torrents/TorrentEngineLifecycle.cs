using Microsoft.Extensions.Logging;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.UseCase;

namespace NiTorrent.Infrastructure.Torrents;

internal sealed class TorrentEngineLifecycle(
    IEngineSettingsService engineSettingsService,
    RestoreSessionUseCase restoreSessionUseCase,
    ITorrentRepository torrentRepository,
    TorrentEngineCoordinator coordinator,
    TorrentRuntimeWorkGate workGate,
    ILogger<TorrentEngineLifecycle> logger) : ITorrentEngineLifecycle
{
    private static readonly TimeSpan TorrentStopTimeout = TimeSpan.FromSeconds(3);

    private readonly IEngineSettingsService _engineSettingsService = engineSettingsService;
    private readonly RestoreSessionUseCase _restoreSessionUseCase = restoreSessionUseCase;
    private readonly ITorrentRepository _torrentRepository = torrentRepository;
    private readonly TorrentEngineCoordinator _coordinator = coordinator;
    private readonly TorrentRuntimeWorkGate _workGate = workGate;
    private readonly ILogger<TorrentEngineLifecycle> _logger = logger;
    private readonly SemaphoreSlim _restoreGate = new(1, 1);
    private int _started;
    private int _restoreStarted;
    private int _stopAcceptingStarted;
    private int _stopStarted;
    private int _flushStarted;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("[Lifecycle] Torrent runtime start requested");
        await _engineSettingsService.InitializeAsync(cancellationToken).ConfigureAwait(false);
        elapsed.Stop();
        _logger.LogInformation("[Lifecycle] Torrent runtime started in {ElapsedMs} ms", elapsed.ElapsedMilliseconds);
    }

    public async Task RestoreSessionAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _restoreStarted) == 1)
            return;

        await _restoreGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_restoreStarted == 1)
                return;

            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("[Lifecycle] Session restore started");
            await _restoreSessionUseCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Exchange(ref _restoreStarted, 1);
            elapsed.Stop();
            _logger.LogInformation("[Lifecycle] Session restore completed in {ElapsedMs} ms", elapsed.ElapsedMilliseconds);
        }
        finally
        {
            _restoreGate.Release();
        }
    }

    public Task StopAcceptingWorkAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.Exchange(ref _stopAcceptingStarted, 1) == 1)
            return Task.CompletedTask;

        _logger.LogInformation("[Lifecycle] Torrent runtime is no longer accepting new work");
        _workGate.StopAcceptingWork();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopStarted, 1) == 1)
            return;

        _logger.LogInformation("[Lifecycle] Torrent runtime stop started");

        foreach (var (_, manager) in _coordinator.GetTorrentMap())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await manager.StopAsync(TorrentStopTimeout).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop torrent manager {TorrentName}", manager.Name);
            }
        }
    }

    public async Task FlushStateAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _flushStarted, 1) == 1)
            return;

        _logger.LogInformation("[Lifecycle] Torrent runtime state flush started");
        await _torrentRepository.FlushAsync(cancellationToken).ConfigureAwait(false);
        _coordinator.DisposeRuntime();
        _logger.LogInformation("[Lifecycle] Torrent runtime state flush completed");
    }
}
