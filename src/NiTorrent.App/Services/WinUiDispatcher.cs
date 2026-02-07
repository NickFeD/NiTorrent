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
        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (ct.CanBeCanceled)
            ct.Register(() => tcs.TrySetCanceled(ct));

        var ok = _queue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!ok)
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue to UI thread."));

        return tcs.Task;
    }
}
