using System.Text.Json;
using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Owns persisted product-owned torrent collection used to render UI instantly on app start.
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
            existing.Intent = entry.Intent;
            existing.HasMetadata = entry.HasMetadata;
            existing.Progress = entry.LastKnownStatus.Progress;
            existing.LastPhase = entry.LastKnownStatus.Phase;
            existing.IsComplete = entry.LastKnownStatus.IsComplete;
            existing.Error = entry.LastKnownStatus.Error;
            existing.SelectedFiles = entry.SelectedFiles?.ToList() ?? new List<string>();
            existing.DeferredActions = entry.DeferredActions?
                .Select(x => new TorrentCatalogDeferredActionEntry
                {
                    Type = x.Type,
                    RequestedAtUtc = x.RequestedAtUtc
                })
                .ToList() ?? new List<TorrentCatalogDeferredActionEntry>();
            existing.PerTorrentSettings = CloneSettingsOrNull(entry.PerTorrentSettings);

            // Keep legacy field out of new saves.
            existing.ShouldRun = null;
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

            entry.Intent = shouldRun ? TorrentIntent.Running : TorrentIntent.Paused;
            entry.ShouldRun = null;

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

            var intent = ResolveIntent(entry);
            return (true, intent == TorrentIntent.Running);
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

    public async Task<RuntimeAttachmentResult> AttachRestoredManagersAsync(
        ClientEngine engine,
        Func<TorrentManager, string> stableKey,
        CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var pendingRemovalByKey = _catalog.PendingRemovals
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var matchedManagers = new List<MatchedRuntimeManager>();
            var pendingRemovals = new List<PendingRemoval>();
            var unmatchedManagers = new List<UnmatchedRuntimeManager>();
            var usedIds = new HashSet<Guid>();

            foreach (var manager in engine.Torrents)
            {
                var key = stableKey(manager);

                if (!string.IsNullOrWhiteSpace(key) && pendingRemovalByKey.TryGetValue(key, out var pendingRemoval))
                {
                    pendingRemovals.Add(new PendingRemoval(manager, key, pendingRemoval.DeleteDownloadedData));
                    continue;
                }

                var matchedEntry = TryFindExistingEntry(manager, key, usedIds);
                if (matchedEntry is null)
                {
                    unmatchedManagers.Add(new UnmatchedRuntimeManager(
                        Key: key,
                        Name: manager.Name,
                        SavePath: manager.SavePath,
                        Manager: manager));
                    continue;
                }

                usedIds.Add(matchedEntry.Id);
                matchedManagers.Add(new MatchedRuntimeManager(new TorrentId(matchedEntry.Id), manager));
            }

            return new RuntimeAttachmentResult(matchedManagers, pendingRemovals, unmatchedManagers);
        }
        finally { _gate.Release(); }
    }

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

        var catalog = await Task.Run(() =>
            JsonSerializer.Deserialize<TorrentCatalog>(json, TorrentCatalog.JsonOptions) ?? new TorrentCatalog(),
            ct).ConfigureAwait(false);

        MigrateToCurrentSchema(catalog);
        return catalog;
    }

    private static void MigrateToCurrentSchema(TorrentCatalog catalog)
    {
        foreach (var entry in catalog.Items)
        {
            entry.Intent ??= entry.ShouldRun == true ? TorrentIntent.Running : TorrentIntent.Paused;
            entry.HasMetadata ??= !string.IsNullOrWhiteSpace(entry.Key) || !string.IsNullOrWhiteSpace(entry.Name);
            entry.SelectedFiles ??= new List<string>();
            entry.DeferredActions ??= new List<TorrentCatalogDeferredActionEntry>();
            if (entry.PerTorrentSettings is { } settings && settings.IsDefault())
                entry.PerTorrentSettings = null;
        }

        catalog.SchemaVersion = 5;
    }

    private TorrentCatalogEntry? TryFindExistingEntry(TorrentManager manager, string key, HashSet<Guid> usedIds)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var byKey = _catalog.Items.FirstOrDefault(x =>
                !usedIds.Contains(x.Id)
                && !string.IsNullOrWhiteSpace(x.Key)
                && string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

            if (byKey is not null)
                return byKey;
        }

        var savePath = manager.SavePath ?? string.Empty;
        var name = manager.Name ?? string.Empty;

        return _catalog.Items.FirstOrDefault(x =>
            !usedIds.Contains(x.Id)
            && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.SavePath, savePath, StringComparison.OrdinalIgnoreCase));
    }

    private static TorrentEntry MapEntry(TorrentCatalogEntry entry)
    {
        var runtime = new TorrentRuntimeState(
            TorrentLifecycleStateMapper.FromPhase(entry.LastPhase),
            entry.IsComplete,
            entry.Progress,
            0,
            0,
            entry.Error,
            false);

        var lastKnownStatus = new TorrentStatus(
            entry.LastPhase,
            entry.IsComplete,
            entry.Progress,
            0,
            0,
            entry.Error,
            TorrentStatusSource.Cached);

        return new TorrentEntry(
            new TorrentId(entry.Id),
            string.IsNullOrWhiteSpace(entry.Key) ? TorrentKey.Empty : new TorrentKey(entry.Key),
            entry.Name,
            entry.Size,
            entry.SavePath,
            entry.AddedAtUtc,
            ResolveIntent(entry),
            runtime.LifecycleState,
            runtime,
            lastKnownStatus,
            HasMetadata: entry.HasMetadata ?? (!string.IsNullOrWhiteSpace(entry.Key) || !string.IsNullOrWhiteSpace(entry.Name)),
            SelectedFiles: entry.SelectedFiles ?? Array.Empty<string>(),
            PerTorrentSettings: CloneSettingsOrNull(entry.PerTorrentSettings),
            DeferredActions: (entry.DeferredActions ?? new List<TorrentCatalogDeferredActionEntry>())
                .Select(x => new DeferredAction(x.Type, x.RequestedAtUtc))
                .ToList());
    }


    private static TorrentEntrySettings? CloneSettingsOrNull(TorrentEntrySettings? settings)
    {
        if (settings is null || settings.IsDefault())
            return null;

        return new TorrentEntrySettings
        {
            DownloadPathOverride = settings.DownloadPathOverride,
            MaximumDownloadRateBytesPerSecond = settings.MaximumDownloadRateBytesPerSecond,
            MaximumUploadRateBytesPerSecond = settings.MaximumUploadRateBytesPerSecond,
            SequentialDownload = settings.SequentialDownload
        };
    }

    private static TorrentIntent ResolveIntent(TorrentCatalogEntry entry)
        => entry.Intent ?? (entry.ShouldRun == true ? TorrentIntent.Running : TorrentIntent.Paused);
}

public sealed record PendingRemoval(TorrentManager Manager, string Key, bool DeleteDownloadedData);
public sealed record MatchedRuntimeManager(TorrentId Id, TorrentManager Manager);
public sealed record UnmatchedRuntimeManager(string Key, string Name, string SavePath, TorrentManager Manager);
public sealed record RuntimeAttachmentResult(
    IReadOnlyList<MatchedRuntimeManager> MatchedManagers,
    IReadOnlyList<PendingRemoval> PendingRemovals,
    IReadOnlyList<UnmatchedRuntimeManager> UnmatchedManagers);
