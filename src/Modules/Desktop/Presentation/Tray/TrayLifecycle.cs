using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.Presentation.Tray;

public enum TrayTransitionResult { Entered, Failed, Cancelled }

/// <summary>Called on the UI thread. Owns retry admission and invalidates stale transitions.</summary>
public sealed class TrayLifecycle(ITrayService tray) : IDisposable
{
    private CancellationTokenSource? _transition;
    private bool _stopped;
    public bool IsInTray { get; private set; }
    public Exception? LastError { get; private set; }

    public async Task<TrayTransitionResult> EnterAsync(Action hideWindow, CancellationToken ct = default)
    {
        if (_stopped || _transition is not null) return TrayTransitionResult.Cancelled;
        using var transition = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _transition = transition;
        LastError = null;
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                transition.Token.ThrowIfCancellationRequested();
                try
                {
                    tray.Show();
                    transition.Token.ThrowIfCancellationRequested();
                    hideWindow();
                    IsInTray = true;
                    return TrayTransitionResult.Entered;
                }
                catch (OperationCanceledException) when (transition.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    LastError = ex;
                    tray.Hide();
                }
                if (attempt == 0) await Task.Delay(500, transition.Token);
            }
            IsInTray = false;
            return TrayTransitionResult.Failed;
        }
        catch (OperationCanceledException) when (transition.IsCancellationRequested)
        {
            tray.Hide();
            IsInTray = false;
            return TrayTransitionResult.Cancelled;
        }
        finally { _transition = null; }
    }

    public void Leave()
    {
        _transition?.Cancel();
        IsInTray = false;
        tray.Hide();
    }

    public void Dispose()
    {
        _stopped = true;
        Leave();
    }
}
