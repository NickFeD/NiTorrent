using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class InMemoryTorrentRuntimeStateSource(ITorrentRuntimeStatusProvider runtimeStatusProvider) : ITorrentRuntimeStateSource, IDisposable
{
    private readonly ITorrentRuntimeStatusProvider _provider = runtimeStatusProvider;

    private readonly Dictionary<Guid, TorrentRuntimeStatus> _statuses = new();
    private readonly HashSet<Guid> _firstEqualProcessed = new();

    private event Func<TorrentRuntimeStateChangedEventArgs, Task>? _changed;
    private int _subscriberCount;

    private CancellationTokenSource? _cts;

    public bool TryGet(Guid torrentId, out TorrentRuntimeStatus status)
        => _statuses.TryGetValue(torrentId, out status!);

    public IReadOnlyDictionary<Guid, TorrentRuntimeStatus> GetSnapshot()
        => new Dictionary<Guid, TorrentRuntimeStatus>(_statuses);

    public void Update(IReadOnlyList<TorrentRuntimeStatus> statuses)
    {
        var incomingById = statuses.ToDictionary(x => x.TorrentId);
        var changed = new List<TorrentRuntimeStatus>();

        foreach (var status in statuses)
        {
            if (ProcessStatus(status))
            {
                changed.Add(status);
            }
        }

        var removedIds = _statuses.Keys
            .Where(id => !incomingById.ContainsKey(id))
            .ToList();

        foreach (var removedId in removedIds)
        {
            _statuses.Remove(removedId);
            _firstEqualProcessed.Remove(removedId);
        }

        if (changed.Count > 0 || removedIds.Count > 0)
        {
            _changed?.Invoke(new TorrentRuntimeStateChangedEventArgs(changed, removedIds));
        }
    }

    private bool ProcessStatus(TorrentRuntimeStatus status)
    {
        if (_statuses.TryGetValue(status.TorrentId, out var existing))
        {
            if (existing == status)
            {
                return _firstEqualProcessed.Add(status.TorrentId);
            }
        }

        _statuses[status.TorrentId] = status;
        _firstEqualProcessed.Remove(status.TorrentId);
        return true;
    }

    public void Subscribe(Func<TorrentRuntimeStateChangedEventArgs, Task> handler)
    {
        _changed += handler;

        if (++_subscriberCount == 1)
        {
            StartPolling();
        }
        else
        {
            _ = TickAsync(CancellationToken.None);
        }
    }

    public async Task UnsubscribeAsync(Func<TorrentRuntimeStateChangedEventArgs, Task> handler)
    {
        _changed -= handler;

        if (_subscriberCount > 0 && --_subscriberCount == 0)
        {
            _changed = null;
            await StopPollingAsync();
        }
    }

    private void StartPolling()
    {
        _cts = new CancellationTokenSource();
        _ = MonitorAsync(_cts.Token);
    }

    private async Task StopPollingAsync()
    {
        if (_cts is null)
            return;

        _cts.Cancel();


        _cts.Dispose();
        _cts = null;
    }

    private async Task MonitorAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await TickAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        IReadOnlyList<TorrentRuntimeStatus> statuses;

        try
        {
            statuses = await _provider.GetAllAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return;
        }

        var incomingById = statuses.ToDictionary(x => x.TorrentId);
        var changed = new List<TorrentRuntimeStatus>();

        foreach (var status in statuses)
        {
            if (ProcessStatus(status))
            {
                changed.Add(status);
            }
        }

        var removedIds = _statuses.Keys
            .Where(id => !incomingById.ContainsKey(id))
            .ToList();

        foreach (var removedId in removedIds)
        {
            _statuses.Remove(removedId);
            _firstEqualProcessed.Remove(removedId);
        }

        if (changed.Count > 0 || removedIds.Count > 0)
        {
            var handlers = _changed;
            if (handlers is not null)
            {
                await handlers.Invoke(
                    new TorrentRuntimeStateChangedEventArgs(changed, removedIds));
            }
        }
    }
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
