using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed class ActivationQueue(
    IServiceProvider services,
    ILogger<ActivationQueue> logger) : IActivationQueue
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<ActivationQueue> _logger = logger;
    private readonly Queue<AppActivationArguments> _pending = new();
    private readonly HashSet<AppActivationArguments> _queued = new(ReferenceComparer.Instance);
    private readonly HashSet<AppActivationArguments> _processed = new(ReferenceComparer.Instance);
    private readonly SemaphoreSlim _drainGate = new(1, 1);
    private readonly object _gate = new();
    private bool _ready;
    private bool _accepting = true;

    public void Enqueue(AppActivationArguments activationArgs)
    {
        ArgumentNullException.ThrowIfNull(activationArgs);

        var shouldDrain = false;

        lock (_gate)
        {
            if (!_accepting)
            {
                _logger.LogWarning("Ignoring activation event because shutdown is in progress");
                return;
            }

            if (_queued.Contains(activationArgs) || _processed.Contains(activationArgs))
            {
                _logger.LogDebug("Ignoring duplicate activation event");
                return;
            }

            _pending.Enqueue(activationArgs);
            _queued.Add(activationArgs);
            shouldDrain = _ready;
        }

        _logger.LogInformation("[Lifecycle] Activation queued: {ActivationKind}", ActivationLogFormatter.Describe(activationArgs));

        if (shouldDrain)
            _ = DrainSafelyAsync();
    }

    public void StopAccepting()
    {
        lock (_gate)
        {
            _accepting = false;
        }

        _logger.LogInformation("[Lifecycle] Activation queue stopped accepting events");
    }

    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _ready = true;
        }

        await _drainGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AppActivationArguments activationArgs;

                lock (_gate)
                {
                    if (_pending.Count == 0)
                        return;

                    activationArgs = _pending.Dequeue();
                    _queued.Remove(activationArgs);

                    if (_processed.Contains(activationArgs))
                        continue;

                    _processed.Add(activationArgs);
                }

                try
                {
                    var elapsed = System.Diagnostics.Stopwatch.StartNew();
                    _logger.LogInformation("[Lifecycle] Activation processing started: {ActivationKind}", ActivationLogFormatter.Describe(activationArgs));
                    var activationService = _services.GetRequiredService<IAppActivationService>();
                    await activationService.HandleAsync(activationArgs).ConfigureAwait(false);
                    elapsed.Stop();
                    _logger.LogInformation(
                        "[Lifecycle] Activation processed in {ElapsedMs} ms: {ActivationKind}",
                        elapsed.ElapsedMilliseconds,
                        ActivationLogFormatter.Describe(activationArgs));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Lifecycle] Activation processing failed: {ActivationKind}", ActivationLogFormatter.Describe(activationArgs));
                }
            }
        }
        finally
        {
            _drainGate.Release();
        }
    }

    private async Task DrainSafelyAsync()
    {
        try
        {
            await DrainAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Activation queue drain failed");
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<AppActivationArguments>
    {
        public static ReferenceComparer Instance { get; } = new();

        public bool Equals(AppActivationArguments? x, AppActivationArguments? y)
            => ReferenceEquals(x, y);

        public int GetHashCode(AppActivationArguments obj)
            => RuntimeHelpers.GetHashCode(obj);
    }
}
