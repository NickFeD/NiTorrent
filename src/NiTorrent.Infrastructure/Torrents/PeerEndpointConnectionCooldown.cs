using System.Collections;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using MonoTorrent.Client;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Tracks connection failures per (torrent, peer endpoint) and temporarily blocks
/// reconnect attempts to noisy endpoints.
/// </summary>
public sealed class PeerEndpointConnectionCooldown
{
    private static readonly TimeSpan FirstTierCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SecondTierCooldown = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ThirdTierCooldown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StaleEndpointTtl = TimeSpan.FromMinutes(30);

    private readonly ILogger<PeerEndpointConnectionCooldown> _logger;
    private readonly object _gate = new();

    private readonly Dictionary<Guid, Dictionary<string, EndpointCooldownState>> _statesByTorrent = new();
    private readonly Dictionary<TorrentManager, Subscription> _subscriptions = new();

    public PeerEndpointConnectionCooldown(ILogger<PeerEndpointConnectionCooldown> logger)
    {
        _logger = logger;
    }

    public void Register(Guid torrentId, TorrentManager manager)
    {
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(manager, out var existing))
            {
                existing.TorrentId = torrentId;
                return;
            }

            var managerType = manager.GetType();
            var failedEvent = managerType.GetEvent("ConnectionAttemptFailed");
            var connectedEvent = managerType.GetEvent("PeerConnected");
            if (failedEvent is null || connectedEvent is null)
                return;

            var failedHandler = CreateEventHandler(
                failedEvent.EventHandlerType!,
                (sender, args) => OnConnectionAttemptFailed(sender as TorrentManager ?? manager, torrentId, args));
            var connectedHandler = CreateEventHandler(
                connectedEvent.EventHandlerType!,
                (sender, args) => OnPeerConnected(sender as TorrentManager ?? manager, torrentId, args));

            failedEvent.AddEventHandler(manager, failedHandler);
            connectedEvent.AddEventHandler(manager, connectedHandler);

            _subscriptions[manager] = new Subscription(torrentId, failedEvent, connectedEvent, failedHandler, connectedHandler);
        }
    }

    public void Unregister(Guid torrentId, TorrentManager manager)
    {
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(manager, out var existing))
            {
                existing.FailedEvent.RemoveEventHandler(manager, existing.FailedHandler);
                existing.ConnectedEvent.RemoveEventHandler(manager, existing.ConnectedHandler);
                _subscriptions.Remove(manager);
            }

            ClearTorrentStateUnsafe(torrentId);
        }
    }

    public void ResetForTorrent(Guid torrentId)
    {
        List<TorrentManager> affectedManagers;
        lock (_gate)
        {
            affectedManagers = _subscriptions
                .Where(x => x.Value.TorrentId == torrentId)
                .Select(x => x.Key)
                .ToList();

            ClearTorrentStateUnsafe(torrentId);
        }

        foreach (var manager in affectedManagers)
            ApplyBanToAllPeers(manager, shouldBan: false);
    }

    private void OnConnectionAttemptFailed(TorrentManager manager, Guid torrentId, object? eventArgs)
    {
        var peer = TryReadProperty(eventArgs, "Peer");
        var endpoint = TryGetEndpointKey(peer);
        if (endpoint is null)
            return;

        var reason = TryReadProperty(eventArgs, "Reason")?.ToString() ?? "unknown";

        DateTimeOffset now;
        int failCount;
        DateTimeOffset cooldownUntil;
        CancellationTokenSource? delayToken;

        lock (_gate)
        {
            now = DateTimeOffset.UtcNow;
            PruneStaleUnsafe(now);

            var byEndpoint = GetOrCreateTorrentMapUnsafe(torrentId);
            if (!byEndpoint.TryGetValue(endpoint, out var state))
            {
                state = new EndpointCooldownState();
                byEndpoint[endpoint] = state;
            }

            state.LastTouchedUtc = now;
            state.FailureCount++;

            var cooldown = GetCooldown(state.FailureCount);
            state.CooldownUntilUtc = now + cooldown;

            state.ReleaseCts?.Cancel();
            state.ReleaseCts?.Dispose();
            state.ReleaseCts = new CancellationTokenSource();

            failCount = state.FailureCount;
            cooldownUntil = state.CooldownUntilUtc;
            delayToken = state.ReleaseCts;
        }

        ApplyBanToPeer(peer, shouldBan: true);
        ApplyBanToEndpointPeers(manager, endpoint, shouldBan: true);
        ScheduleCooldownRelease(manager, torrentId, endpoint, delayToken, cooldownUntil);

        _logger.LogDebug(
            "Peer endpoint cooldown applied. Torrent={TorrentId}; Endpoint={Endpoint}; FailCount={FailCount}; CooldownUntilUtc={CooldownUntilUtc}; Reason={Reason}",
            torrentId,
            endpoint,
            failCount,
            cooldownUntil,
            reason);
    }

    private void OnPeerConnected(TorrentManager manager, Guid torrentId, object? eventArgs)
    {
        var peer = TryReadProperty(eventArgs, "Peer");
        var endpoint = TryGetEndpointKey(peer);
        if (endpoint is null)
            return;

        lock (_gate)
        {
            if (!_statesByTorrent.TryGetValue(torrentId, out var byEndpoint))
                return;

            if (!byEndpoint.TryGetValue(endpoint, out var state))
                return;

            state.ReleaseCts?.Cancel();
            state.ReleaseCts?.Dispose();
            byEndpoint.Remove(endpoint);
        }

        ApplyBanToEndpointPeers(manager, endpoint, shouldBan: false);
    }

    private void ScheduleCooldownRelease(
        TorrentManager manager,
        Guid torrentId,
        string endpoint,
        CancellationTokenSource cts,
        DateTimeOffset cooldownUntilUtc)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var delay = cooldownUntilUtc - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                cts.Dispose();
            }

            var shouldRelease = false;
            lock (_gate)
            {
                if (_statesByTorrent.TryGetValue(torrentId, out var byEndpoint)
                    && byEndpoint.TryGetValue(endpoint, out var state)
                    && ReferenceEquals(state.ReleaseCts, cts)
                    && state.CooldownUntilUtc <= DateTimeOffset.UtcNow)
                {
                    state.ReleaseCts = null;
                    shouldRelease = true;
                }
            }

            if (shouldRelease)
                ApplyBanToEndpointPeers(manager, endpoint, shouldBan: false);
        });
    }

    private void ClearTorrentStateUnsafe(Guid torrentId)
    {
        if (!_statesByTorrent.TryGetValue(torrentId, out var byEndpoint))
            return;

        foreach (var state in byEndpoint.Values)
        {
            state.ReleaseCts?.Cancel();
            state.ReleaseCts?.Dispose();
        }

        _statesByTorrent.Remove(torrentId);
    }

    private Dictionary<string, EndpointCooldownState> GetOrCreateTorrentMapUnsafe(Guid torrentId)
    {
        if (_statesByTorrent.TryGetValue(torrentId, out var existing))
            return existing;

        var created = new Dictionary<string, EndpointCooldownState>(StringComparer.OrdinalIgnoreCase);
        _statesByTorrent[torrentId] = created;
        return created;
    }

    private void PruneStaleUnsafe(DateTimeOffset now)
    {
        var emptyTorrents = new List<Guid>();

        foreach (var (torrentId, byEndpoint) in _statesByTorrent)
        {
            var staleEndpoints = byEndpoint
                .Where(x => now - x.Value.LastTouchedUtc >= StaleEndpointTtl)
                .Select(x => x.Key)
                .ToList();

            foreach (var endpoint in staleEndpoints)
            {
                var state = byEndpoint[endpoint];
                state.ReleaseCts?.Cancel();
                state.ReleaseCts?.Dispose();
                byEndpoint.Remove(endpoint);
            }

            if (byEndpoint.Count == 0)
                emptyTorrents.Add(torrentId);
        }

        foreach (var torrentId in emptyTorrents)
            _statesByTorrent.Remove(torrentId);
    }

    private static TimeSpan GetCooldown(int failureCount)
        => failureCount switch
        {
            <= 2 => FirstTierCooldown,
            <= 4 => SecondTierCooldown,
            _ => ThirdTierCooldown
        };

    private static Delegate CreateEventHandler(Type delegateType, Action<object?, object?> callback)
    {
        var invoke = delegateType.GetMethod("Invoke")
                     ?? throw new InvalidOperationException($"Delegate type {delegateType.FullName} has no Invoke method.");
        var parameters = invoke.GetParameters()
            .Select(p => Expression.Parameter(p.ParameterType, p.Name))
            .ToArray();

        if (parameters.Length < 2)
            throw new InvalidOperationException($"Delegate type {delegateType.FullName} is expected to have at least 2 parameters.");

        var callbackInvoke = callback.GetType().GetMethod(nameof(Action<object?, object?>.Invoke))
                             ?? throw new InvalidOperationException("Cannot resolve callback invoke method.");

        var callbackCall = Expression.Call(
            Expression.Constant(callback),
            callbackInvoke,
            Expression.Convert(parameters[0], typeof(object)),
            Expression.Convert(parameters[1], typeof(object)));

        Expression body = invoke.ReturnType switch
        {
            _ when invoke.ReturnType == typeof(void)
                => callbackCall,
            _ when invoke.ReturnType == typeof(Task)
                => Expression.Block(callbackCall, Expression.Property(null, typeof(Task), nameof(Task.CompletedTask))),
            _ when invoke.ReturnType.IsValueType
                => Expression.Block(callbackCall, Expression.Default(invoke.ReturnType)),
            _
                => Expression.Block(callbackCall, Expression.Constant(null, invoke.ReturnType))
        };

        return Expression.Lambda(delegateType, body, parameters).Compile();
    }

    private static void ApplyBanToEndpointPeers(TorrentManager manager, string endpoint, bool shouldBan)
    {
        var peers = GetManagerPeers(manager);
        if (peers.Count == 0)
            return;

        foreach (var peer in peers)
        {
            var peerEndpoint = TryGetEndpointKey(peer);
            if (!string.Equals(peerEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                continue;

            ApplyBanToPeer(peer, shouldBan);
        }
    }

    private static void ApplyBanToAllPeers(TorrentManager manager, bool shouldBan)
    {
        var peers = GetManagerPeers(manager);
        foreach (var peer in peers)
            ApplyBanToPeer(peer, shouldBan);
    }

    private static List<object> GetManagerPeers(TorrentManager manager)
    {
        try
        {
            var peersProperty = manager.GetType().GetProperty("Peers");
            var value = peersProperty?.GetValue(manager);
            if (value is null)
                return [];

            if (value is IEnumerable enumerable)
            {
                var peers = new List<object>();
                foreach (var item in enumerable)
                {
                    if (item is not null)
                        peers.Add(item);
                }

                return peers;
            }
        }
        catch
        {
            // best effort only
        }

        return [];
    }

    private static void ApplyBanToPeer(object? peer, bool shouldBan)
    {
        if (peer is null)
            return;

        try
        {
            var type = peer.GetType();
            var property = type.GetProperty(
                "IsBanned",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);

            if (property is null || property.PropertyType != typeof(bool))
                return;

            var setter = property.SetMethod ?? property.GetSetMethod(nonPublic: true);
            if (setter is null)
                return;

            setter.Invoke(peer, [shouldBan]);
        }
        catch
        {
            // best effort only
        }
    }

    private static object? TryReadProperty(object? source, string propertyName)
    {
        if (source is null)
            return null;

        var property = source.GetType().GetProperty(propertyName);
        return property?.GetValue(source);
    }

    private static string? TryGetEndpointKey(object? peerLike)
    {
        if (peerLike is null)
            return null;

        if (TryReadUri(peerLike, "ConnectionUri", out var connectionUri))
            return NormalizeEndpoint(connectionUri);

        if (TryReadUri(peerLike, "Uri", out var uri))
            return NormalizeEndpoint(uri);

        if (TryReadEndpoint(peerLike, "EndPoint", out var endpoint))
            return endpoint;

        if (TryReadEndpoint(peerLike, "ConnectionEndPoint", out var connectionEndpoint))
            return connectionEndpoint;

        return null;
    }

    private static bool TryReadUri(object source, string propertyName, out Uri uri)
    {
        uri = null!;
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return false;

        if (property.GetValue(source) is Uri value)
        {
            uri = value;
            return true;
        }

        return false;
    }

    private static bool TryReadEndpoint(object source, string propertyName, out string endpoint)
    {
        endpoint = string.Empty;
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return false;

        var value = property.GetValue(source);
        if (value is null)
            return false;

        endpoint = value.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(endpoint);
    }

    private static string NormalizeEndpoint(Uri uri)
        => $"{uri.Host}:{uri.Port}";

    private sealed class EndpointCooldownState
    {
        public int FailureCount { get; set; }
        public DateTimeOffset CooldownUntilUtc { get; set; }
        public DateTimeOffset LastTouchedUtc { get; set; }
        public CancellationTokenSource? ReleaseCts { get; set; }
    }

    private sealed class Subscription
    {
        public Guid TorrentId { get; set; }
        public System.Reflection.EventInfo FailedEvent { get; }
        public System.Reflection.EventInfo ConnectedEvent { get; }
        public Delegate FailedHandler { get; }
        public Delegate ConnectedHandler { get; }

        public Subscription(
            Guid torrentId,
            System.Reflection.EventInfo failedEvent,
            System.Reflection.EventInfo connectedEvent,
            Delegate failedHandler,
            Delegate connectedHandler)
        {
            TorrentId = torrentId;
            FailedEvent = failedEvent;
            ConnectedEvent = connectedEvent;
            FailedHandler = failedHandler;
            ConnectedHandler = connectedHandler;
        }
    }
}
