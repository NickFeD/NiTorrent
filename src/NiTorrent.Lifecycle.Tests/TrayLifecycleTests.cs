using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Tray;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class TrayLifecycleTests
{
    [Fact]
    public async Task WindowIsHiddenOnlyAfterSuccessfulRegistration()
    {
        var adapter = new TrayAdapter();
        using var lifecycle = new TrayLifecycle(adapter);
        var hidden = false;
        var result = await lifecycle.EnterAsync(() => { Assert.True(adapter.Registered); hidden = true; });
        Assert.Equal(TrayTransitionResult.Entered, result);
        Assert.True(hidden);
        Assert.True(lifecycle.IsInTray);
        Assert.Equal(1, adapter.Attempts);
        lifecycle.Leave();
        Assert.False(adapter.Registered);
        Assert.False(lifecycle.IsInTray);
    }

    [Fact]
    public async Task FailedFirstRegistrationIsCleanedAndRetriedBeforeHiding()
    {
        var adapter = new TrayAdapter { Failures = 1 };
        using var lifecycle = new TrayLifecycle(adapter);
        var hides = 0;
        var result = await lifecycle.EnterAsync(() => hides++);
        Assert.Equal(TrayTransitionResult.Entered, result);
        Assert.Equal(2, adapter.Attempts);
        Assert.Equal(1, adapter.Cleanups);
        Assert.Equal(1, hides);
    }

    [Fact]
    public async Task TwoFailuresKeepWindowAccessibleAndAllowLaterUserRequest()
    {
        var adapter = new TrayAdapter { Failures = 2 };
        using var lifecycle = new TrayLifecycle(adapter);
        var hides = 0;
        Assert.Equal(TrayTransitionResult.Failed, await lifecycle.EnterAsync(() => hides++));
        Assert.False(lifecycle.IsInTray);
        Assert.False(adapter.Registered);
        Assert.Equal(2, adapter.Attempts);
        Assert.Equal(0, hides);
        Assert.IsType<IOException>(lifecycle.LastError);
        Assert.Equal(TrayTransitionResult.Entered, await lifecycle.EnterAsync(() => hides++));
        Assert.Equal(3, adapter.Attempts);
        Assert.Equal(1, hides);
    }

    [Fact]
    public async Task ReturningToWindowDuringRetryCancelsStaleRegistration()
    {
        var adapter = new TrayAdapter { Failures = 1 };
        using var lifecycle = new TrayLifecycle(adapter);
        var hides = 0;
        var pending = lifecycle.EnterAsync(() => hides++);
        lifecycle.Leave();
        Assert.Equal(TrayTransitionResult.Cancelled, await pending);
        Assert.Equal(1, adapter.Attempts);
        Assert.Equal(0, hides);
        Assert.False(adapter.Registered);
    }

    [Fact]
    public async Task ShutdownDuringRetryPreventsFurtherAttemptsAndNewAdmission()
    {
        var adapter = new TrayAdapter { Failures = 2 };
        var lifecycle = new TrayLifecycle(adapter);
        var pending = lifecycle.EnterAsync(() => throw new InvalidOperationException("Must stay visible"));
        lifecycle.Dispose();
        Assert.Equal(TrayTransitionResult.Cancelled, await pending);
        Assert.Equal(TrayTransitionResult.Cancelled, await lifecycle.EnterAsync(() => { }));
        Assert.Equal(1, adapter.Attempts);
        Assert.False(adapter.Registered);
    }

    [Fact]
    public async Task ConcurrentRequestsShareOneRetrySequence()
    {
        var adapter = new TrayAdapter { Failures = 1 };
        using var lifecycle = new TrayLifecycle(adapter);
        var pending = lifecycle.EnterAsync(() => { });
        Assert.Equal(TrayTransitionResult.Cancelled, await lifecycle.EnterAsync(() => throw new Exception("Duplicate hide")));
        Assert.Equal(TrayTransitionResult.Entered, await pending);
        Assert.Equal(2, adapter.Attempts);
    }

    [Fact]
    public async Task ExplorerRecoveryUsesSameBoundedRetriesAndMarksFailure()
    {
        var adapter = new TrayAdapter();
        using var lifecycle = new TrayLifecycle(adapter);
        await lifecycle.EnterAsync(() => { });
        adapter.Hide(); // The shell lost the registered icon.
        adapter.Failures = 3;
        Assert.Equal(TrayTransitionResult.Failed, await lifecycle.EnterAsync(() => { }));
        Assert.Equal(3, adapter.Attempts);
        Assert.False(lifecycle.IsInTray);
        Assert.False(adapter.Registered);
    }

    private sealed class TrayAdapter : ITrayService
    {
        public int Attempts;
        public int Failures;
        public int Cleanups;
        public bool Registered;
        public event Action? OpenRequested { add { } remove { } }
        public event Func<Task>? ExitRequested { add { } remove { } }
        public event Action? RecoveryRequested { add { } remove { } }
        public event Action<int>? BulkFailed { add { } remove { } }
        public void Show()
        {
            Attempts++;
            Registered = true; // Simulate a failure after partial native registration.
            if (Attempts <= Failures) throw new IOException("Shell unavailable");
        }
        public void Hide() { Cleanups++; Registered = false; }
        public void Dispose() => Hide();
    }
}
