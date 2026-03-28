using System.Text.Json;
using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Owns persisted torrent list (JSON) used to render UI instantly on app start.
/// Thread-safe: all access to the in-memory catalog is guarded by _gate.
/// </summary>
public sealed class TorrentCatalogStore
{
    private readonly ILogger _logger;
    private readonly string _catalogFilePath;

    // Guards both in-memory catalog and file writes.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DateTimeOffset _lastSaveUtc = DateTimeOffset.MinValue;

    // In-memory catalog. Loaded once lazily.
    private TorrentCatalog _catalog = new();
    private bool _loaded;

    public TorrentCatalogStore(ILogger<TorrentCatalogStore> logger, IAppStorageService storage)
    {
        _logger = logger;
        _catalogFilePath = storage.GetLocalPath(@"Torrents\torrent_catalog.json");
        storage.EnsureParentDirectory(_catalogFilePath);
    }

    /// <summary>
    /// Ensures catalog is loaded into memory exactly once.
    /// Call this early, but it is safe to call multiple times.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loaded) return;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_loaded) return;

            _catalog = await LoadCatalogAsync(ct).ConfigureAwait(false);
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }


    public async Task<IReadOnlyList<TorrentEntry>> GetEntriesAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _catalog.Items
                .OrderByDescending(x => x.AddedAtUtc)
                .Select(MapEntry)
                .ToList();
        }
        finally { _gate.Release(); }
    }

    public async Task<TorrentEntry?> TryGetEntryAsync(TorrentId id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entry = _catalog.Items.FirstOrDefault(x => x.Id == id.Value);
            return entry is null ? null : MapEntry(entry);
        }
        finally { _gate.Release(); }
    }

    public async Task<TorrentEntry?> TryGetEntryByKeyAsync(TorrentKey key, CancellationToken ct = default)
    {
        if (key.IsEmpty)
            return null;

        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entry = _catalog.Items.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Key)
                && string.Equals(x.Key, key.Value, StringComparison.OrdinalIgnoreCase));
            return entry is null ? null : MapEntry(entry);
        }
        finally { _gate.Release(); }
    }

    public async Task UpsertEntryAsync(TorrentEntry entry, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var existing = _catalog.Items.FirstOrDefault(x => x.Id == entry.Id.Value);
            if (existing is null)
            {
                existing = new TorrentCatalogEntry { Id = entry.Id.Value };
                _catalog.Items.Add(existing);
            }

            existing.Key = entry.Key.IsEmpty ? existing.Key : entry.Key.Value;
            existing.Name = entry.Name;
            existing.Size = entry.Size;
            existing.SavePath = entry.SavePath;
            existing.AddedAtUtc = entry.AddedAtUtc;
            existing.Progress = entry.LastKnownStatus.Progress;
            existing.LastPhase = entry.LastKnownStatus.Phase;
            existing.IsComplete = entry.LastKnownStatus.IsComplete;
            existing.ShouldRun = entry.Intent == TorrentIntent.Running;
        }
        finally { _gate.Release(); }
    }

    public Task RemoveEntryAsync(TorrentId id, CancellationToken ct = default) =>
        RemoveAsync(id, ct);

    public async Task SetShouldRunAsync(TorrentId id, bool shouldRun, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entry = _catalog.Items.FirstOrDefault(x => x.Id == id.Value);
            if (entry is null) return;

            entry.ShouldRun = shouldRun;

            if (!shouldRun && entry.LastPhase is TorrentPhase.Downloading or TorrentPhase.Seeding or TorrentPhase.Checking or TorrentPhase.FetchingMetadata or TorrentPhase.WaitingForEngine)
                entry.LastPhase = TorrentPhase.Paused;
        }
        finally { _gate.Release(); }
    }

    public async Task<(bool found, bool shouldRun)> TryGetShouldRunAsync(TorrentId id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entry = _catalog.Items.FirstOrDefault(x => x.Id == id.Value);
            if (entry is null) return (false, false);

            return (true, entry.ShouldRun);
        }
        finally { _gate.Release(); }
    }

    public async Task<DateTimeOffset?> TryGetAddedAtUtcAsync(TorrentId id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entry = _catalog.Items.FirstOrDefault(x => x.Id == id.Value);
            return entry?.AddedAtUtc;
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(TorrentId id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _catalog.Items.RemoveAll(x => x.Id == id.Value);
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAndRememberDeletionAsync(TorrentId id, string? key, bool deleteDownloadedData, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _catalog.Items.RemoveAll(x => x.Id == id.Value);

            if (string.IsNullOrWhiteSpace(key))
                return;

            var existing = _catalog.PendingRemovals.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                _catalog.PendingRemovals.Add(new TorrentPendingRemovalEntry
                {
                    Key = key,
                    DeleteDownloadedData = deleteDownloadedData
                });
            }
            else
            {
                existing.DeleteDownloadedData = existing.DeleteDownloadedData || deleteDownloadedData;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<PendingRemoval>> AttachRestoredManagersAsync(
        ClientEngine engine,
        Dictionary<TorrentId, TorrentManager> byId,
        Func<TorrentManager, string> stableKey,
        CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            byId.Clear();

            var pendingRemovalByKey = _catalog.PendingRemovals
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var byKey = _catalog.Items
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var pendingRemovals = new List<PendingRemoval>();
            var usedIds = new HashSet<Guid>();

            foreach (var m in engine.Torrents)
            {
                var key = stableKey(m);

                if (!string.IsNullOrWhiteSpace(key) && pendingRemovalByKey.TryGetValue(key, out var pendingRemoval))
                {
                    pendingRemovals.Add(new PendingRemoval(m, key, pendingRemoval.DeleteDownloadedData));
                    continue;
                }

                var entry = TryMatchCatalogEntry(m, key, byKey, usedIds);
                var matchedExistingEntry = entry is not null;
                if (entry is null)
                {
                    entry = new TorrentCatalogEntry
                    {
                        Id = Guid.NewGuid(),
                        AddedAtUtc = DateTimeOffset.UtcNow,
                        ShouldRun = m.State is not TorrentState.Stopped and not TorrentState.Paused
                    };
                    _catalog.Items.Add(entry);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        if (!byKey.TryGetValue(key, out var bucket))
                        {
                            bucket = new List<TorrentCatalogEntry>();
                            byKey[key] = bucket;
                        }
                        bucket.Add(entry);
                    }
                }

                entry.Key = string.IsNullOrWhiteSpace(key) ? entry.Key : key;
                entry.Name = m.Name;
                entry.Size = m.Torrent?.Size ?? 0;
                entry.SavePath = m.SavePath;
                if (entry.AddedAtUtc == default)
                    entry.AddedAtUtc = DateTimeOffset.UtcNow;
                if (!matchedExistingEntry)
                    entry.ShouldRun = m.State is not TorrentState.Stopped and not TorrentState.Paused;

                var id = new TorrentId(entry.Id);
                if (!byId.ContainsKey(id))
                {
                    byId[id] = m;
                    usedIds.Add(entry.Id);
                }
                else
                {
                    var fallback = CreateFallbackEntry(m, key);
                    _catalog.Items.Add(fallback);
                    var fallbackId = new TorrentId(fallback.Id);
                    byId[fallbackId] = m;
                    usedIds.Add(fallback.Id);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        if (!byKey.TryGetValue(key, out var bucket))
                        {
                            bucket = new List<TorrentCatalogEntry>();
                            byKey[key] = bucket;
                        }
                        bucket.Add(fallback);
                    }
                }
            }

            return pendingRemovals;
        }
        finally { _gate.Release(); }
    }

    private static TorrentCatalogEntry? TryMatchCatalogEntry(
        TorrentManager manager,
        string key,
        Dictionary<string, List<TorrentCatalogEntry>> byKey,
        HashSet<Guid> usedIds)
    {
        if (string.IsNullOrWhiteSpace(key) || !byKey.TryGetValue(key, out var candidates))
            return null;

        return candidates.FirstOrDefault(x => !usedIds.Contains(x.Id) &&
                                              string.Equals(x.SavePath, manager.SavePath, StringComparison.OrdinalIgnoreCase) &&
                                              string.Equals(x.Name, manager.Name, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(x => !usedIds.Contains(x.Id));
    }

    private static TorrentCatalogEntry CreateFallbackEntry(TorrentManager manager, string key)
        => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Name = manager.Name,
            Size = manager.Torrent?.Size ?? 0,
            SavePath = manager.SavePath,
            AddedAtUtc = DateTimeOffset.UtcNow,
            ShouldRun = manager.State is not TorrentState.Stopped and not TorrentState.Paused
        };

    public async Task CompletePendingRemovalAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _catalog.PendingRemovals.RemoveAll(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(bool force, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        if (!force && (now - _lastSaveUtc) < TimeSpan.FromSeconds(5))
            return;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!force && (now - _lastSaveUtc) < TimeSpan.FromSeconds(5))
                return;

            var json = JsonSerializer.Serialize(_catalog, TorrentCatalog.JsonOptions);
            await File.WriteAllTextAsync(_catalogFilePath, json, ct).ConfigureAwait(false);
            _lastSaveUtc = now;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save torrent catalog");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<TorrentCatalog> LoadCatalogAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_catalogFilePath))
            return new TorrentCatalog();

        var json = await File.ReadAllTextAsync(_catalogFilePath, ct).ConfigureAwait(false);

        return await Task.Run(() =>
            JsonSerializer.Deserialize<TorrentCatalog>(json, TorrentCatalog.JsonOptions) ?? new TorrentCatalog(),
            ct).ConfigureAwait(false);
    }


    private static TorrentEntry MapEntry(TorrentCatalogEntry entry)
    {
        var runtime = new TorrentRuntimeState(
            TorrentLifecycleStateMapper.FromPhase(entry.LastPhase),
            entry.IsComplete,
            entry.Progress,
            0,
            0,
            null,
            false);

        var lastKnownStatus = new TorrentStatus(
            entry.LastPhase,
            entry.IsComplete,
            entry.Progress,
            0,
            0,
            null,
            TorrentStatusSource.Cached);

        return new TorrentEntry(
            new TorrentId(entry.Id),
            string.IsNullOrWhiteSpace(entry.Key) ? TorrentKey.Empty : new TorrentKey(entry.Key),
            entry.Name,
            entry.Size,
            entry.SavePath,
            entry.AddedAtUtc,
            entry.ShouldRun ? TorrentIntent.Running : TorrentIntent.Paused,
            runtime.LifecycleState,
            runtime,
            lastKnownStatus,
            HasMetadata: true,
            SelectedFiles: Array.Empty<string>(),
            PerTorrentSettings: null,
            DeferredActions: Array.Empty<DeferredAction>());
    }

}

public sealed record PendingRemoval(TorrentManager Manager, string Key, bool DeleteDownloadedData);
