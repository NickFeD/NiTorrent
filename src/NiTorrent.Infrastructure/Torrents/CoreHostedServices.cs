using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using System.Diagnostics;

namespace NiTorrent.Infrastructure.Torrents;

internal sealed class CoreShutdownState { public bool Succeeded { get; set; } = true; }

internal abstract class CoreHostedService : IHostedLifecycleService
{
    public virtual Task StartingAsync(CancellationToken ct) => Task.CompletedTask;
    public virtual Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public virtual Task StartedAsync(CancellationToken ct) => Task.CompletedTask;
    public virtual Task StoppingAsync(CancellationToken ct) => Task.CompletedTask;
    public virtual Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    public virtual Task StoppedAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class StorageHostedService(
    AppSettingsService settings, ISettingsRepository settingsRepository, ITorrentRepository repository,
    IEnumerable<IShutdownStateService> markers, CoreShutdownState shutdown,
    ILogger<StorageHostedService> logger) : CoreHostedService
{
    private bool _loaded;
    public override async Task StartingAsync(CancellationToken ct)
    {
        var elapsed = Stopwatch.StartNew();
        foreach (var marker in markers)
        {
            logger.LogInformation("Previous shutdown: {State}", await marker.ReadPreviousStateAsync(ct));
            await marker.MarkStartedAsync(ct);
        }
        await settings.InitializeAsync(ct).ConfigureAwait(false);
        await repository.LoadingAsync(ct).ConfigureAwait(false);
        _loaded = true;
        logger.LogInformation("[Lifecycle] Collection ready in {ElapsedMs} ms", elapsed.ElapsedMilliseconds);
    }
    public override async Task StopAsync(CancellationToken ct)
    {
        if (!_loaded) return;
        try
        {
            await settingsRepository.FlushAsync(ct).ConfigureAwait(false);
            await repository.FlushAsync(ct).ConfigureAwait(false);
            if (shutdown.Succeeded)
                foreach (var marker in markers) await marker.MarkCleanShutdownAsync(ct).ConfigureAwait(false);
        }
        catch { shutdown.Succeeded = false; throw; }
    }
}

internal sealed class TorrentSessionHostedService(
    TorrentSession session, ITorrentEngineLifecycle engine,
    CoreShutdownState shutdown) : CoreHostedService, IDisposable
{
    private Task? _worker;
    public override Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _worker = Task.Run(() => session.RunAsync(CancellationToken.None), CancellationToken.None);
        return Task.CompletedTask;
    }
    public override Task StoppingAsync(CancellationToken ct)
    {
        session.RequestStop();
        return Task.CompletedTask;
    }
    public override async Task StopAsync(CancellationToken ct)
    {
        session.RequestStop();
        try
        {
            if (_worker is not null) await _worker.WaitAsync(ct).ConfigureAwait(false);
            await session.DrainCommandsAsync(ct).ConfigureAwait(false);
            await session.WaitForOperationsAsync(ct).ConfigureAwait(false);
            await engine.StopAcceptingWorkAsync(ct).ConfigureAwait(false);
            await engine.StopAsync(ct).ConfigureAwait(false);
            engine.DisposeRuntime();
        }
        catch { shutdown.Succeeded = false; throw; }
    }
    public void Dispose()
    {
        session.RequestStop();
        if ((_worker is null || _worker.IsCompleted) && session.WaitForOperationsAsync(CancellationToken.None).IsCompletedSuccessfully)
            engine.DisposeRuntime();
    }
}
