namespace NiTorrent.Domain.Torrents;

/// <summary>
/// Product-owned per-torrent settings. These settings belong to the user's torrent entry,
/// not to the engine implementation.
/// </summary>
public sealed class TorrentEntrySettings
{
    private string? _downloadPathOverride;
    private int? _maximumDownloadRateBytesPerSecond;
    private int? _maximumUploadRateBytesPerSecond;

    public static TorrentEntrySettings Default { get; } = new();

    /// <summary>
    /// Null means "inherit global default download path".
    /// </summary>
    public string? DownloadPathOverride
    {
        get => _downloadPathOverride;
        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _downloadPathOverride = null;
                return;
            }

            _downloadPathOverride = new SavePath(value).Value;
        }
    }

    /// <summary>
    /// Null means "inherit global limit".
    /// </summary>
    public int? MaximumDownloadRateBytesPerSecond
    {
        get => _maximumDownloadRateBytesPerSecond;
        init => _maximumDownloadRateBytesPerSecond = ValidateLimit(value, nameof(MaximumDownloadRateBytesPerSecond));
    }

    /// <summary>
    /// Null means "inherit global limit".
    /// </summary>
    public int? MaximumUploadRateBytesPerSecond
    {
        get => _maximumUploadRateBytesPerSecond;
        init => _maximumUploadRateBytesPerSecond = ValidateLimit(value, nameof(MaximumUploadRateBytesPerSecond));
    }

    /// <summary>
    /// Reserved foundation for future per-torrent behavior tuning.
    /// </summary>
    public bool SequentialDownload { get; init; }

    public static TorrentEntrySettings Create(
        string? downloadPathOverride = null,
        int? maximumDownloadRateBytesPerSecond = null,
        int? maximumUploadRateBytesPerSecond = null,
        bool sequentialDownload = false)
        => new()
        {
            DownloadPathOverride = downloadPathOverride,
            MaximumDownloadRateBytesPerSecond = maximumDownloadRateBytesPerSecond,
            MaximumUploadRateBytesPerSecond = maximumUploadRateBytesPerSecond,
            SequentialDownload = sequentialDownload
        };

    public bool IsDefault()
        => string.IsNullOrWhiteSpace(DownloadPathOverride)
           && MaximumDownloadRateBytesPerSecond is null
           && MaximumUploadRateBytesPerSecond is null
           && !SequentialDownload;

    private static int? ValidateLimit(int? value, string paramName)
    {
        if (value is null)
            return null;

        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, "Per-torrent rate limits cannot be negative.");

        return value;
    }
}
