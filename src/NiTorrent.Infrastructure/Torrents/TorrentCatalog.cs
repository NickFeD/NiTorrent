using System.Text.Json;
using System.Text.Json.Serialization;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Persisted list of torrents shown by UI on app start (even before the torrent engine is ready).
/// This makes startup smooth: UI can render the list immediately, then MonoTorrent “attaches” later.
/// </summary>
internal sealed class TorrentCatalog
{
    public int SchemaVersion { get; set; } = 3;
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

    // Cached UI data (last known state from previous run)
    public double Progress { get; set; }
    public TorrentPhase LastPhase { get; set; } = TorrentPhase.Stopped;
    public bool IsComplete { get; set; }

    /// <summary>
    /// User intent: should this torrent be running.
    /// If true, and the torrent engine is still starting, UI can show "waiting" state.
    /// After the engine is ready, those torrents will be started automatically.
    /// </summary>
    public bool ShouldRun { get; set; }
}


internal sealed class TorrentPendingRemovalEntry
{
    public string Key { get; set; } = "";
    public bool DeleteDownloadedData { get; set; }
}
