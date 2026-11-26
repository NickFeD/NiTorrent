using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NiTorrent.Torrents.Contracts;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>One consumer, one callback at a time, at most one pending current state.</summary>
internal sealed class RuntimeObservation : IDisposable
{
    private readonly Action<RuntimeObservation> _remove;
    private readonly Func<TorrentObservationSnapshot, CancellationToken, Task> _observer;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancel;
    private readonly Channel<TorrentObservationSnapshot> _pending = Channel.CreateBounded<TorrentObservationSnapshot>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, AllowSynchronousContinuations = false });
    private int _disposed;
    public TorrentObservationRequest Request { get; }
    public RuntimeObservation(TorrentObservationRequest request,
        Func<TorrentObservationSnapshot, CancellationToken, Task> observer,
        Action<RuntimeObservation> remove, CancellationToken shutdown, ILogger logger)
    {
        Request = request; _observer = observer; _remove = remove; _logger = logger;
        _cancel = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
    }
    public void Publish(TorrentObservationSnapshot snapshot) => _pending.Writer.TryWrite(snapshot);
    public Task RunAsync() => Task.Run(async () =>
    {
        try
        {
            await foreach (var snapshot in _pending.Reader.ReadAllAsync(_cancel.Token).ConfigureAwait(false))
            {
                _cancel.Token.ThrowIfCancellationRequested();
                try { await _observer(snapshot, _cancel.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { return; }
                catch (Exception ex) { _logger.LogWarning(ex, "Status observer failed"); }
            }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        finally { _cancel.Dispose(); }
    });
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _remove(this);
        _pending.Writer.TryComplete();
        // Delivery may already have ended during host cancellation.
        try { _cancel.Cancel(); } catch (ObjectDisposedException) { }
    }
}
