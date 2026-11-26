using Microsoft.Extensions.Logging;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentSession(
    ITorrentRepository repository,
    ITorrentRuntimeGateway runtime,
    ITorrentEngineLifecycle engine,
    ILogger<TorrentSession> logger) : ITorrentSession, IDisposable
{
    private readonly object _gate = new();
    private readonly Queue<Guid> _normal = new();
    private readonly Queue<Guid> _priority = new();
    private readonly Dictionary<Guid, bool> _queued = new();
    private readonly Dictionary<Guid, long> _versions = new();
    private readonly Dictionary<Guid, TorrentPreparation> _preparations = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource _engineReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _accepting = true;
    private int _priorityCount;
    private Guid? _inFlight;
    private string? _engineError;
    private int _disposed;
    private int _operationCount;
    private TaskCompletionSource _operationsIdle = CompletedSignal();

    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }

    public async Task<T> UseEngineAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        lock (_gate)
        {
            if (!_accepting) throw new InvalidOperationException("Application is stopping.");
            if (_operationCount++ == 0) _operationsIdle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopping.Token);
        try
        {
            await WaitForEngineAsync(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            return await operation(linked.Token).ConfigureAwait(false);
        }
        finally { lock (_gate) if (--_operationCount == 0) _operationsIdle.TrySetResult(); }
    }

    public Task WaitForOperationsAsync(CancellationToken ct)
    {
        lock (_gate) return _operationsIdle.Task.WaitAsync(ct);
    }

    public async Task DrainCommandsAsync(CancellationToken ct)
    {
        // RequestStop closes admission first. Finish the last admitted persistence operation
        // before final flush/disposal, including its cancellation cleanup.
        await _commands.WaitAsync(ct).ConfigureAwait(false);
        _commands.Release();
    }

    public string? EngineError { get { lock (_gate) return _engineError; } }
    public event Action? EngineStateChanged;
    public bool IsEngineReady => _engineReady.Task.IsCompletedSuccessfully;
    private void PublishEngineState()
    {
        if (EngineStateChanged is not { } changed) return;
        foreach (Action observer in changed.GetInvocationList())
        {
            try { observer(); }
            catch (Exception ex) { logger.LogWarning(ex, "Engine state observer failed"); }
        }
    }
    public Task WaitForEngineAsync(CancellationToken ct) => _engineReady.Task.WaitAsync(ct);
    public IReadOnlyDictionary<Guid, TorrentPreparation> GetPreparations()
    {
        lock (_gate) return new Dictionary<Guid, TorrentPreparation>(_preparations);
    }

    public async Task AddAsync(TorrentDownload item, TorrentSource source, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopping.Token);
        await _commands.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ThrowIfStopping();
            await repository.AddAsync(item, source, linked.Token).ConfigureAwait(false);
            Schedule(item.Id);
        }
        finally { _commands.Release(); }
    }

    public async Task SetStateAsync(Guid id, TorrentDownloadStatus state, CancellationToken ct)
    {
        if (state is not (TorrentDownloadStatus.Running or TorrentDownloadStatus.Paused))
            throw new ArgumentOutOfRangeException(nameof(state));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopping.Token);
        await _commands.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ThrowIfStopping();
            var item = await repository.GetByIdAsync(id, linked.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Torrent not found.");
            if (item.Status == TorrentDownloadStatus.Deleted)
                throw new InvalidOperationException("Torrent is being deleted.");
            if (state == TorrentDownloadStatus.Running) item.Start();
            else if (item.Status != TorrentDownloadStatus.Paused) item.Pause();
            await repository.UpdateAsync(item, linked.Token).ConfigureAwait(false);
            Schedule(id);
        }
        finally { _commands.Release(); }
    }

    public async Task DeleteAsync(Guid id, bool deleteFiles, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopping.Token);
        await _commands.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ThrowIfStopping();
            var item = await repository.GetByIdAsync(id, linked.Token).ConfigureAwait(false);
            if (item is null) return;
            item.MarkDeleted();
            item.DeleteFiles = deleteFiles;
            await repository.UpdateAsync(item, linked.Token).ConfigureAwait(false);
            // Even an in-flight Add must subsequently observe this tombstone.
            SetPreparation(id, new(TorrentPreparationState.Deleting));
            bool inFlight;
            lock (_gate) inFlight = _inFlight == id;
            if (!deleteFiles && !inFlight && !await runtime.ExistsByIdAsync(id).ConfigureAwait(false))
            {
                await repository.DeleteAsync(id, linked.Token).ConfigureAwait(false);
                lock (_gate) _preparations.Remove(id);
            }
            Schedule(id);
        }
        finally { _commands.Release(); }
    }

    public void Retry(Guid id)
    {
        lock (_gate)
        {
            if (!_accepting) return;
            _preparations[id] = new(TorrentPreparationState.Failed);
        }
        Schedule(id);
    }

    public void Schedule(Guid id, bool priority = true)
    {
        lock (_gate)
        {
            if (!_accepting) return;
            _versions[id] = _versions.GetValueOrDefault(id) + 1;
            if (!_preparations.ContainsKey(id)) _preparations[id] = new(TorrentPreparationState.Waiting);
            if (_queued.TryGetValue(id, out var wasPriority) && (wasPriority || !priority)) return;
            _queued[id] = priority;
            (priority ? _priority : _normal).Enqueue(id);
            _signal.Release();
        }
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopping.Token);
        var token = linked.Token;
        try
        {
            foreach (var item in await repository.GetAllAsync(token).ConfigureAwait(false)) Schedule(item.Id, false);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    await engine.StartAsync(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    _engineReady.TrySetResult();
                    PublishEngineState();
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Engine start attempt {Attempt} failed", attempt + 1);
                    if (attempt == 0) continue;
                    lock (_gate) _engineError = "Не удалось запустить движок. Попробуйте сброс настроек или переустановку приложения.";
                    _engineReady.TrySetException(ex);
                    _ = _engineReady.Task.Exception;
                    PublishEngineState();
                    return;
                }
            }
            while (true)
            {
                await _signal.WaitAsync(token).ConfigureAwait(false);
                Guid id;
                lock (_gate)
                {
                    if (!TryTake(out id)) continue;
                    _inFlight = id;
                }
                try { await ReconcileAsync(id, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Torrent preparation failed for {TorrentId}", id);
                    SetPreparation(id, new(TorrentPreparationState.Failed, ex.Message));
                }
                finally { lock (_gate) _inFlight = null; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_gate) _engineError = ex.Message;
            _engineReady.TrySetException(ex);
            _ = _engineReady.Task.Exception;
            PublishEngineState();
            logger.LogError(ex, "Torrent session failed");
        }
        finally { _engineReady.TrySetCanceled(); }
    }

    private bool TryTake(out Guid id)
    {
        // Discard entries superseded by promotion without counting them against fairness.
        while (_normal.TryPeek(out var normal) && (!_queued.TryGetValue(normal, out var p) || p)) _normal.Dequeue();
        while (_priority.TryPeek(out var high) && (!_queued.TryGetValue(high, out var p) || !p)) _priority.Dequeue();
        var usePriority = _priority.Count > 0 && (_priorityCount < 4 || _normal.Count == 0);
        var queue = usePriority ? _priority : _normal;
        if (!queue.TryDequeue(out id)) return false;
        _queued.Remove(id);
        _priorityCount = usePriority ? _priorityCount + 1 : 0;
        return true;
    }

    private async Task ReconcileAsync(Guid id, CancellationToken ct)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        long version;
        lock (_gate) version = _versions.GetValueOrDefault(id);
        var item = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        var exists = await runtime.ExistsByIdAsync(id).ConfigureAwait(false);
        if (item is null)
        {
            if (exists) await runtime.RemoveAsync(id, false, ct).ConfigureAwait(false);
            lock (_gate) _preparations.Remove(id);
            return;
        }
        bool failed;
        lock (_gate) failed = _preparations.TryGetValue(id, out var previous) && previous.State == TorrentPreparationState.Failed;
        if (failed && exists)
        {
            await runtime.RemoveAsync(id, false, ct).ConfigureAwait(false);
            exists = false;
        }
        SetPreparation(id, new(item.Status == TorrentDownloadStatus.Deleted
            ? TorrentPreparationState.Deleting : TorrentPreparationState.Preparing));
        try
        {
            if (!exists && (item.Status != TorrentDownloadStatus.Deleted || item.DeleteFiles))
            {
                var stored = await repository.GetForRestoreAsync(id, ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Torrent source not found.");
                ct.ThrowIfCancellationRequested();
                await runtime.AddAsync(id, stored.TorrentSource, item.SavePath, ct).ConfigureAwait(false);
            }
            // Re-read after Add: commands accepted while metadata was loading take precedence.
            item = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
            if (item is null) return;
            ct.ThrowIfCancellationRequested();
            if (item.Status == TorrentDownloadStatus.Deleted)
            {
                if (await runtime.ExistsByIdAsync(id).ConfigureAwait(false))
                    await runtime.RemoveAsync(id, item.DeleteFiles, ct).ConfigureAwait(false);
                await repository.DeleteAsync(id, ct).ConfigureAwait(false);
                lock (_gate) _preparations.Remove(id);
                logger.LogInformation("[Lifecycle] Torrent {TorrentId} removed in {ElapsedMs} ms", id, elapsed.ElapsedMilliseconds);
                return;
            }
            await runtime.UpdateFileSelectionAsync(id, item.FileEntries, ct).ConfigureAwait(false);
            item = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
            if (item is null) return;
            if (item.Status == TorrentDownloadStatus.Running)
                await runtime.StartAsync(id, ct).ConfigureAwait(false);
            else if (item.Status != TorrentDownloadStatus.Deleted)
                await runtime.PauseAsync(id, ct).ConfigureAwait(false);
            SetPreparation(id, new(TorrentPreparationState.Ready));
            logger.LogInformation("[Lifecycle] Torrent {TorrentId} ready in {ElapsedMs} ms; intent={Intent}",
                id, elapsed.ElapsedMilliseconds, item.Status);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // A retry must not skip the selection/start stages of a partially added manager.
            if (await runtime.ExistsByIdAsync(id).ConfigureAwait(false))
            {
                try { await runtime.RemoveAsync(id, false, ct).ConfigureAwait(false); }
                catch (Exception cleanup) { logger.LogWarning(cleanup, "Partial manager cleanup failed for {TorrentId}", id); }
            }
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (_accepting && _versions.GetValueOrDefault(id) != version && !_queued.ContainsKey(id))
                {
                    _queued[id] = true;
                    _priority.Enqueue(id);
                    _signal.Release();
                }
            }
        }
    }

    private void SetPreparation(Guid id, TorrentPreparation state) { lock (_gate) _preparations[id] = state; }
    private void ThrowIfStopping() { lock (_gate) if (!_accepting) throw new InvalidOperationException("Application is stopping."); }
    public void RequestStop()
    {
        lock (_gate)
        {
            if (!_accepting) return;
            _accepting = false;
        }
        _stopping.Cancel();
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        RequestStop();
        _stopping.Dispose();
        _signal.Dispose();
        _commands.Dispose();
    }
}
