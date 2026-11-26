using Microsoft.UI.Dispatching;
using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services;

public sealed class WinUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _queue;

    public WinUiDispatcher(DispatcherQueue queue)
        => _queue = queue;

    public bool TryEnqueue(Action action)
        => _queue.TryEnqueue(() => action());

    public Task EnqueueAsync(Action action, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return Task.FromCanceled(ct);
        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        var ok = _queue.TryEnqueue(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                action();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally { registration.Dispose(); }
        });

        if (!ok)
        {
            registration.Dispose();
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue to UI thread."));
        }

        return tcs.Task;
    }
}
