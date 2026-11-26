using System.Collections.ObjectModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Torrents.Contracts;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>One host-owned collector enabled by demand. The torrent session never depends on it.</summary>
public sealed class InMemoryTorrentRuntimeStateSource(ITorrentRuntimeStatusProvider provider,
    ILogger<InMemoryTorrentRuntimeStateSource> logger) : BackgroundService, ITorrentRuntimeStateSource, ITorrentObservations
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _changed = new(0, 1);
    private readonly HashSet<RuntimeObservation> _observations = [];
    private readonly HashSet<Task> _deliveries = [];
    private readonly CancellationTokenSource _shutdown = new();
    private IReadOnlyDictionary<Guid, TorrentRuntimeStatus> _statuses = Empty();
    private TorrentObservationSnapshot _latest = new(Empty(), 0, 0, TorrentConnectionState.Waiting);
    private bool _stopping;

    private static IReadOnlyDictionary<Guid, TorrentRuntimeStatus> Empty() =>
        new ReadOnlyDictionary<Guid, TorrentRuntimeStatus>(new Dictionary<Guid, TorrentRuntimeStatus>());
    public bool TryGet(Guid id, out TorrentRuntimeStatus status)
    {
        lock (_gate) return _statuses.TryGetValue(id, out status!);
    }
    public IReadOnlyDictionary<Guid, TorrentRuntimeStatus> GetSnapshot()
    {
        lock (_gate) return _statuses;
    }
    public IDisposable Observe(TorrentObservationRequest request,
        Func<TorrentObservationSnapshot, CancellationToken, Task> observer)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(observer);
        if (!Enum.IsDefined(request.Kind)) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.Kind is TorrentObservationKind.Torrent or TorrentObservationKind.Peers or TorrentObservationKind.Trackers
            && (request.TorrentId is null || request.TorrentId == Guid.Empty))
            throw new ArgumentException("A torrent id is required.", nameof(request));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            var observation = new RuntimeObservation(request, observer, Remove, _shutdown.Token, logger);
            _observations.Add(observation);
            observation.Publish(Project(request, _latest));
            var delivery = observation.RunAsync();
            _deliveries.Add(delivery);
            _ = delivery.ContinueWith(completed => { lock (_gate) _deliveries.Remove(completed); },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            Signal();
            return observation;
        }
    }
    private void Remove(RuntimeObservation observation)
    {
        lock (_gate) _observations.Remove(observation);
        Signal();
    }
    private void Signal()
    {
        try { _changed.Release(); } catch (SemaphoreFullException) { }
    }
    private static bool Supported(TorrentObservationKind kind) =>
        kind is TorrentObservationKind.Summary or TorrentObservationKind.Collection or TorrentObservationKind.Torrent;
    private static TorrentObservationSnapshot Project(TorrentObservationRequest request, TorrentObservationSnapshot snapshot)
    {
        if (!Supported(request.Kind))
            return new(Empty(), 0, 0, TorrentConnectionState.Unsupported, "Эти данные пока не поддерживаются выбранным адаптером движка.");
        if (request.Kind == TorrentObservationKind.Summary) return snapshot with { Statuses = Empty() };
        if (request.Kind == TorrentObservationKind.Torrent)
            return snapshot with { Statuses = new ReadOnlyDictionary<Guid, TorrentRuntimeStatus>(
                snapshot.Statuses.Where(pair => pair.Key == request.TorrentId).ToDictionary()) };
        return snapshot;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                stoppingToken.ThrowIfCancellationRequested();
                _changed.Wait(0);
                RuntimeObservation[] observers;
                lock (_gate) observers = _observations.Where(o => Supported(o.Request.Kind)).ToArray();
                if (observers.Length == 0)
                {
                    await _changed.WaitAsync(stoppingToken).ConfigureAwait(false);
                    continue;
                }
                try { Publish(await CollectAsync(observers, stoppingToken).ConfigureAwait(false)); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Status observation failed; will retry while observed");
                    TorrentObservationSnapshot stale;
                    lock (_gate) stale = _latest with { Connection = TorrentConnectionState.Disconnected, Message = "Связь потеряна" };
                    Publish(stale);
                }
                await _changed.WaitAsync(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    private async Task<TorrentObservationSnapshot> CollectAsync(RuntimeObservation[] observers, CancellationToken ct)
    {
        if (observers.All(o => o.Request.Kind == TorrentObservationKind.Summary) && provider is ITorrentSummaryProvider summary)
        {
            var totals = await summary.GetSummaryAsync(ct).ConfigureAwait(false);
            return new(GetSnapshot(), totals.DownloadSpeed, totals.UploadSpeed, TorrentConnectionState.Connected);
        }
        var statuses = await provider.GetAllAsync(ct).ConfigureAwait(false);
        var snapshot = new ReadOnlyDictionary<Guid, TorrentRuntimeStatus>(statuses.ToDictionary(s => s.TorrentId));
        lock (_gate) _statuses = snapshot;
        return new(snapshot, statuses.Sum(s => s.DownloadSpeed), statuses.Sum(s => s.UploadSpeed), TorrentConnectionState.Connected);
    }
    private void Publish(TorrentObservationSnapshot snapshot)
    {
        lock (_gate)
        {
            _latest = snapshot;
            foreach (var observation in _observations) observation.Publish(Project(observation.Request, snapshot));
        }
    }
    public override async Task StopAsync(CancellationToken ct)
    {
        RuntimeObservation[] observations;
        Task[] deliveries;
        lock (_gate)
        {
            _stopping = true;
            observations = _observations.ToArray();
            deliveries = _deliveries.ToArray();
        }
        _shutdown.Cancel();
        foreach (var observation in observations) observation.Dispose();
        await base.StopAsync(ct).ConfigureAwait(false);
        await Task.WhenAll(deliveries).WaitAsync(ct).ConfigureAwait(false);
    }
    public override void Dispose()
    {
        lock (_gate) _stopping = true;
        _shutdown.Cancel();
        base.Dispose();
    }
}
