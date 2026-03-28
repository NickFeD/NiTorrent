using System.Text.Json;
using System.Text.Json.Serialization;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Persisted product-owned torrent collection shown by UI on app start.
/// Runtime engine state may attach to these entries later, but it does not define the collection.
/// </summary>
internal sealed class TorrentCatalog
{
    public int SchemaVersion { get; set; } = 4;
    public List<TorrentCatalogEntry> Items { get; set; } = new();
    public List<TorrentPendingRemovalEntry> PendingRemovals { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}

internal sealed class TorrentCatalogEntry
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable key which allows matching restored MonoTorrent managers to persisted entries.
    /// Usually this is a hex InfoHash (v1) or similar.
    /// </summary>
    public string? Key { get; set; }

    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string SavePath { get; set; } = "";
    public DateTimeOffset AddedAtUtc { get; set; }

    // Persisted product state.
    public TorrentIntent? Intent { get; set; }
    public bool? HasMetadata { get; set; }
    public string? Error { get; set; }
    public List<string> SelectedFiles { get; set; } = new();
    public List<TorrentCatalogDeferredActionEntry> DeferredActions { get; set; } = new();

    // Cached UI data (last known state from previous run).
    public double Progress { get; set; }
    public TorrentPhase LastPhase { get; set; } = TorrentPhase.Stopped;
    public bool IsComplete { get; set; }

    /// <summary>
    /// Legacy v3 field kept only for migration. New saves write Intent instead.
    /// </summary>
    public bool? ShouldRun { get; set; }
}

internal sealed class TorrentCatalogDeferredActionEntry
{
    public DeferredActionType Type { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
}

internal sealed class TorrentPendingRemovalEntry
{
    public string Key { get; set; } = "";
    public bool DeleteDownloadedData { get; set; }
}
