using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>The host owns polling. Subscribers only observe snapshots.</summary>
public sealed class InMemoryTorrentRuntimeStateSource(
    ITorrentRuntimeStatusProvider provider,
    ILogger<InMemoryTorrentRuntimeStateSource> logger) : BackgroundService, ITorrentRuntimeStateSource
{
    private readonly object _gate = new();
    private Dictionary<Guid, TorrentRuntimeStatus> _statuses = [];
    private readonly HashSet<Func<TorrentRuntimeStateChangedEventArgs, Task>> _handlers = [];

    public bool TryGet(Guid torrentId, out TorrentRuntimeStatus status)
    {
        lock (_gate) return _statuses.TryGetValue(torrentId, out status!);
    }
    public IReadOnlyDictionary<Guid, TorrentRuntimeStatus> GetSnapshot()
    {
        lock (_gate) return new Dictionary<Guid, TorrentRuntimeStatus>(_statuses);
    }
    public void Subscribe(Func<TorrentRuntimeStateChangedEventArgs, Task> handler)
    {
        lock (_gate) _handlers.Add(handler);
    }
    public Task UnsubscribeAsync(Func<TorrentRuntimeStateChangedEventArgs, Task> handler)
    {
        lock (_gate) _handlers.Remove(handler);
        return Task.CompletedTask;
    }
    public void Update(IReadOnlyList<TorrentRuntimeStatus> statuses)
    {
        Func<TorrentRuntimeStateChangedEventArgs, Task>[] handlers;
        TorrentRuntimeStateChangedEventArgs change;
        lock (_gate)
        {
            var next = statuses.ToDictionary(s => s.TorrentId);
            var updated = statuses.Where(s => !_statuses.TryGetValue(s.TorrentId, out var old) || old != s).ToList();
            var removed = _statuses.Keys.Where(id => !next.ContainsKey(id)).ToList();
            _statuses = next;
            if (updated.Count == 0 && removed.Count == 0) return;
            change = new(updated, removed);
            handlers = _handlers.ToArray();
        }
        foreach (var handler in handlers) _ = NotifyAsync(handler, change);
    }
    private async Task NotifyAsync(Func<TorrentRuntimeStateChangedEventArgs, Task> handler, TorrentRuntimeStateChangedEventArgs change)
    {
        try { await handler(change).ConfigureAwait(false); }
        catch (Exception ex) { logger.LogWarning(ex, "Status observer failed"); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { Update(await provider.GetAllAsync(stoppingToken).ConfigureAwait(false)); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Status polling failed"); }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
