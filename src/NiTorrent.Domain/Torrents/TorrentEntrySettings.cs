namespace NiTorrent.Domain.Torrents;

/// <summary>
/// Product-owned per-torrent settings. These settings belong to the user's torrent entry,
/// not to the engine implementation.
/// </summary>
public sealed class TorrentEntrySettings
{
    public static TorrentEntrySettings Default { get; } = new();

    /// <summary>
    /// Null means "inherit global default download path".
    /// </summary>
    public string? DownloadPathOverride { get; init; }

    /// <summary>
    /// Null means "inherit global limit".
    /// </summary>
    public int? MaximumDownloadRateBytesPerSecond { get; init; }

    /// <summary>
    /// Null means "inherit global limit".
    /// </summary>
    public int? MaximumUploadRateBytesPerSecond { get; init; }

    /// <summary>
    /// Reserved foundation for future per-torrent behavior tuning.
    /// </summary>
    public bool SequentialDownload { get; init; }

    public bool IsDefault()
        => string.IsNullOrWhiteSpace(DownloadPathOverride)
           && MaximumDownloadRateBytesPerSecond is null
           && MaximumUploadRateBytesPerSecond is null
           && !SequentialDownload;
}
